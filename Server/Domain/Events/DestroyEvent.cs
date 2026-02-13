using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Identifiers;
using System;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// A committable event representing the intent to destroy a card.
    /// </summary>
    [GameEventMetadata("破坏", "当一张卡牌被破坏时触发的事件。", EventType.Destroy)]
    public class DestroyEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("摧毁原因", "卡牌被摧毁的原因。")]
        public EventReasonSource Reason { get; }

        [GameEventParameter("摧毁来源", "导致摧毁的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? DestroySource => Source?.Card;

        [GameEventParameter("被摧毁的卡牌", "被摧毁的目标卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? DestroyTarget => Target.FirstOrDefault()?.Card;

        [GameEventParameter("摧毁来源快照", "生成事件时记录的摧毁来源快照。", Scope = GameEventParameterScope.Input)]
        public CardInstanceSnapshot? DestroySourceSnapshot { get; }

        [GameEventParameter("被摧毁目标快照", "生成事件时记录的被摧毁目标快照。", Scope = GameEventParameterScope.Input)]
        public CardInstanceSnapshot? DestroyTargetSnapshot { get; }

        public bool DestroyApplied { get; private set; }

        public DestroyEvent(CardInstance target, CardInstance source, EventReasonSource reason, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Destroy, eventId, timestamp)
        {
            Target.Add(TargetInfo.ForCard(target));
            Source = TargetInfo.ForCard(source);
            Reason = reason;
            DestroyTargetSnapshot = CardInstanceSnapshot.From(target);
            DestroySourceSnapshot = CardInstanceSnapshot.From(source);
        }

        public DomainOperationResult Commit(GameContext context)
        {
            DestroyApplied = false;
            var targetCard = DestroyTarget;
            var sourceCard = DestroySource;

            if (targetCard == null || sourceCard == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Target or source card is null in DestroyEvent." };
            }

            var targetKeywords = context.BuffService.GetEffectiveKeywords(targetCard, context);
            if (targetKeywords.Contains(CardKeyword.DestroyResist) && Reason == EventReasonSource.Ability)
            {
                return new DomainOperationResult { Success = true };
            }

            if (targetKeywords.Contains(CardKeyword.EarthSigil)
                && Reason == EventReasonSource.Ability)
            {
                return new DomainOperationResult { Success = true };
            }

            if (targetCard.IsSuperEvolved &&
                targetCard.IsFollower &&
                targetCard.OwnerPlayerId == context.CurrentGameState.CurrentPlayerId &&
                Reason == EventReasonSource.Ability)
            {
                return new DomainOperationResult { Success = true };
            }

            var ownerPlayer = context.CurrentGameState.GetPlayerState(targetCard.OwnerPlayerId);
            if (ownerPlayer == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Owner player for card {targetCard.InstanceId} not found." };
            }

            DestroyApplied = true;
            var moveReason = ResolveMoveReason(Reason);
            var destroyResult = ownerPlayer.DestroyCard(targetCard, sourceCard, Reason, moveReason);
            return destroyResult;
        }

        public string GetAnnouncement(GameState gameState)
        {
            if (!DestroyApplied)
            {
                return string.Empty;
            }

            var target = DestroyTarget;
            if (target == null)
            {
                return string.Empty;
            }

            var source = DestroySource;
            return source != null
                ? $"{AnnouncementTokens.Card(source)}破坏了{AnnouncementTokens.Card(target)}。"
                : $"{AnnouncementTokens.Card(target)}被破坏。";
        }

        private static CardMoveReason ResolveMoveReason(EventReasonSource reason)
        {
            return CardMoveReason.Destroyed;
        }
    }
}
