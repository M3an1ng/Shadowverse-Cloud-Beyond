using System;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Unified pre-routing event for leave-field intents.
    /// It evaluates immunity and emits one legacy leave event (or no-op).
    /// </summary>
    [GameEventMetadata("离场", "统一离场判定与分流事件。", EventType.LeaveField, visibility: EventMetadataVisibility.System)]
    public class LeaveFieldEvent : DelayedEventBase, ICommittableEvent
    {
        [GameEventParameter("离场意图", "请求的离场方式。", Scope = GameEventParameterScope.Input)]
        public LeaveFieldKind RequestedKind { get; }

        [GameEventParameter("离场结果", "最终路由到的离场方式。", Scope = GameEventParameterScope.Output)]
        public LeaveFieldKind RoutedKind { get; private set; } = LeaveFieldKind.NoOp;

        [GameEventParameter("离场来源类型", "离场来源（系统/能力）。", Scope = GameEventParameterScope.Input)]
        public EventReasonSource ReasonSource { get; }

        [GameEventParameter("离场前区域", "触发离场判定时记录的原始区域。", Scope = GameEventParameterScope.Input)]
        public CardZone OriginalZone { get; }

        [GameEventParameter("离场来源", "导致离场的来源卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? LeaveSource => Source?.Card;

        [GameEventParameter("离场目标", "需要离场的目标卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? LeaveTarget => Target.FirstOrDefault()?.Card;

        [GameEventParameter("离场来源快照", "生成事件时记录的来源快照。", Scope = GameEventParameterScope.Input)]
        public CardInstanceSnapshot? LeaveSourceSnapshot { get; }

        [GameEventParameter("离场目标快照", "生成事件时记录的目标快照。", Scope = GameEventParameterScope.Input)]
        public CardInstanceSnapshot? LeaveTargetSnapshot { get; }

        public LeaveFieldEvent(
            CardInstance targetCard,
            LeaveFieldKind requestedKind,
            EventReasonSource reasonSource = EventReasonSource.Ability,
            CardInstance? sourceCard = null,
            CardZone? originalZone = null,
            DelayConfig? delayConfig = null,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.LeaveField, eventId, timestamp)
        {
            Target.Add(TargetInfo.ForCard(targetCard));
            LeaveTargetSnapshot = TryCreateSnapshot(targetCard);

            if (sourceCard != null)
            {
                Source = TargetInfo.ForCard(sourceCard);
                LeaveSourceSnapshot = TryCreateSnapshot(sourceCard);
            }

            RequestedKind = requestedKind;
            ReasonSource = reasonSource;
            OriginalZone = originalZone ?? targetCard.CurrentZone;
            ConfigureDelay(delayConfig ?? DelayConfig.Disabled);
        }

        public DomainOperationResult Commit(GameContext context)
        {
            RoutedKind = LeaveFieldKind.NoOp;
            var targetCard = LeaveTarget;
            if (targetCard == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "LeaveFieldEvent.Commit: target card is null." };
            }

            // If the card has already moved since intent creation, skip legacy routing.
            if (targetCard.CurrentZone != OriginalZone)
            {
                return new DomainOperationResult { Success = true };
            }

            RoutedKind = ResolveRoutedKind(context, targetCard);
            var routedEvent = CreateRoutedEvent(context, targetCard, RoutedKind);
            if (routedEvent == null)
            {
                return new DomainOperationResult { Success = true };
            }

            var result = new DomainOperationResult { Success = true };
            result.GeneratedEvents.Add(routedEvent);
            return result;
        }

        private LeaveFieldKind ResolveRoutedKind(GameContext context, CardInstance targetCard)
        {
            if (RequestedKind == LeaveFieldKind.NoOp)
            {
                return LeaveFieldKind.NoOp;
            }

            var effectiveKind = RequestedKind;

            if (RequestedKind == LeaveFieldKind.Vanish
                && ReasonSource == EventReasonSource.Ability
                && targetCard.CurrentZone == CardZone.Field
                && TargetHasVanishResist(context, targetCard))
            {
                effectiveKind = LeaveFieldKind.NoOp;
            }

            if (RequestedKind == LeaveFieldKind.Vanish
                && ReasonSource == EventReasonSource.System
                && targetCard.CurrentZone == CardZone.Field
                && TargetHasVanishResist(context, targetCard))
            {
                effectiveKind = LeaveFieldKind.Destroy;
            }

            if (effectiveKind == LeaveFieldKind.Destroy
                && ReasonSource == EventReasonSource.Ability
                && !targetCard.CanBeDestroyed(context, ReasonSource))
            {
                return LeaveFieldKind.NoOp;
            }

            return effectiveKind;
        }

        private IGameEvent? CreateRoutedEvent(GameContext context, CardInstance targetCard, LeaveFieldKind routedKind)
        {
            return routedKind switch
            {
                LeaveFieldKind.Destroy => new DestroyEvent(
                    targetCard,
                    LeaveSource ?? targetCard,
                    ReasonSource,
                    destroyTargetSnapshot: LeaveTargetSnapshot,
                    destroySourceSnapshot: LeaveSourceSnapshot),
                LeaveFieldKind.Vanish => new CardVanishedEvent(
                    targetCard,
                    OriginalZone,
                    ReasonSource,
                    LeaveSource,
                    vanishedCardSnapshot: LeaveTargetSnapshot,
                    vanishSourceSnapshot: LeaveSourceSnapshot),
                LeaveFieldKind.Return => CreateReturnEvent(context, targetCard),
                LeaveFieldKind.Tuck => new TuckEvent(
                    targetCard,
                    ReasonSource,
                    tuckedCardSnapshot: LeaveTargetSnapshot,
                    tuckSourceSnapshot: LeaveSourceSnapshot),
                _ => null
            };
        }

        private IGameEvent CreateReturnEvent(GameContext context, CardInstance targetCard)
        {
            var owner = context.CurrentGameState.GetPlayerState(targetCard.OwnerPlayerId);
            return owner != null
                ? new ReturnEvent(
                    owner,
                    targetCard,
                    ReasonSource,
                    returnedCardSnapshot: LeaveTargetSnapshot,
                    returnSourceSnapshot: LeaveSourceSnapshot)
                : new ReturnEvent(
                    targetCard,
                    ReasonSource,
                    returnedCardSnapshot: LeaveTargetSnapshot,
                    returnSourceSnapshot: LeaveSourceSnapshot);
        }

        private static bool TargetHasVanishResist(GameContext context, CardInstance targetCard)
        {
            var keywords = context.BuffService.GetEffectiveKeywords(targetCard, context);
            return keywords.Contains(CardKeyword.VanishResist);
        }

        private static CardInstanceSnapshot? TryCreateSnapshot(CardInstance? card)
        {
            if (card == null)
            {
                return null;
            }

            try
            {
                return CardInstanceSnapshot.From(card);
            }
            catch
            {
                return null;
            }
        }
    }
}
