using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Identifiers;
using System;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// A committable event representing the intent to destroy a card.
    /// </summary>
    [GameEventMetadata("破坏", "当一张卡牌被破坏时触发的事件。", EventType.Destroy)]
    public class DestroyEvent : DelayedEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("破坏原因", "卡牌被破坏的原因。")]
        public EventReasonSource Reason { get; }

        [GameEventParameter("破坏来源", "导致破坏的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? DestroySource => Source?.Card;

        [GameEventParameter("被破坏的卡牌", "被破坏的目标卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? DestroyTarget => Target.FirstOrDefault()?.Card;

        [GameEventParameter("破坏来源快照", "生成事件时记录的破坏来源快照。", Scope = GameEventParameterScope.Input)]
        public CardInstanceSnapshot? DestroySourceSnapshot { get; }

        [GameEventParameter("被破坏目标快照", "生成事件时记录的被破坏目标快照。", Scope = GameEventParameterScope.Input)]
        public CardInstanceSnapshot? DestroyTargetSnapshot { get; }

        public DestroyEvent(
            CardInstance target,
            CardInstance source,
            EventReasonSource reason,
            Guid? eventId = null,
            DateTime? timestamp = null,
            CardInstanceSnapshot? destroyTargetSnapshot = null,
            CardInstanceSnapshot? destroySourceSnapshot = null)
            : base(EventType.Destroy, eventId, timestamp)
        {
            Target.Add(TargetInfo.ForCard(target));
            Source = TargetInfo.ForCard(source);
            Reason = reason;
            DestroyTargetSnapshot = destroyTargetSnapshot ?? TryCreateSnapshot(target);
            DestroySourceSnapshot = destroySourceSnapshot ?? TryCreateSnapshot(source);
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var targetCard = DestroyTarget;
            var sourceCard = DestroySource;

            if (targetCard == null || sourceCard == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Target or source card is null in DestroyEvent." };
            }

            if (!targetCard.CanBeDestroyed(context, Reason))
            {
                return new DomainOperationResult { Success = true };
            }

            var ownerPlayer = context.CurrentGameState.GetPlayerState(targetCard.OwnerPlayerId);
            if (ownerPlayer == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Owner player for card {targetCard.InstanceId} not found." };
            }

            var moveReason = ResolveMoveReason(Reason);
            var destroyResult = ownerPlayer.DestroyCard(targetCard, sourceCard, Reason, moveReason);
            if (destroyResult.Success)
            {
                TryApplyAttackFlowLeaderPing(context, destroyResult, targetCard, sourceCard);
            }
            return destroyResult;
        }

        public string GetAnnouncement(GameState gameState)
        {

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

        private void TryApplyAttackFlowLeaderPing(
            GameContext context,
            DomainOperationResult destroyResult,
            CardInstance targetCard,
            CardInstance sourceCard)
        {
            var attackFlow = context.AttackFlow;
            if (attackFlow == null)
            {
                return;
            }

            if (targetCard.CardType != CardType.Follower)
            {
                return;
            }

            if (targetCard.InstanceId != attackFlow.DefenderId || sourceCard.InstanceId != attackFlow.AttackerId)
            {
                return;
            }

            var attackerIsSuperEvolved = DestroySourceSnapshot?.IsSuperEvolved == true || sourceCard.IsSuperEvolved;
            if (!attackerIsSuperEvolved)
            {
                return;
            }

            if (!attackFlow.TryMarkScoredDestroyedTarget(targetCard.InstanceId))
            {
                return;
            }

            var defenderOwner = context.CurrentGameState.GetPlayerState(targetCard.OwnerPlayerId);
            if (defenderOwner?.Leader == null)
            {
                return;
            }

            destroyResult.GeneratedEvents.Add(new DamageEvent(defenderOwner.Leader, 1, sourceCard, DamageKind.Ability));
        }
    }
}
