using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Identifiers;
using System;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// A committable event representing the intent to destroy a card.
    /// </summary>
    [GameEventMetadata("摧毁卡牌", "当一张卡牌被摧毁时触发的事件。", EventType.Destroy)]
    public class DestroyEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("摧毁原因", "卡牌被摧毁的原因。")]
        public string Reason { get; }

        [GameEventParameter("摧毁来源", "导致摧毁的卡牌。")]
        public CardInstance? DestroySource => Source?.Card;

        [GameEventParameter("被摧毁的卡牌", "被摧毁的目标卡牌。")]
        public CardInstance? DestroyTarget => Target.FirstOrDefault()?.Card;

        public DestroyEvent(CardInstance target, CardInstance source, string reason)
            : base(EventType.Destroy)
        {
            Target.Add(TargetInfo.ForCard(target));
            Source = TargetInfo.ForCard(source);
            Reason = reason;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var targetCard = DestroyTarget;
            var sourceCard = DestroySource;

            if (targetCard == null || sourceCard == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Target or source card is null in DestroyEvent." };
            }

            var targetKeywords = context.BuffService.GetEffectiveKeywords(targetCard, context);
            if (targetKeywords.Contains(CardKeyword.DestroyResist) && IsAbilityDestroyReason(Reason))
            {
                return new DomainOperationResult { Success = true };
            }

            if (targetCard.IsSuperEvolved &&
                targetCard.IsFollower &&
                targetCard.OwnerPlayerId == context.CurrentGameState.CurrentPlayerId &&
                IsAbilityDestroyReason(Reason))
            {
                return new DomainOperationResult { Success = true };
            }

            var ownerPlayer = context.CurrentGameState.GetPlayerState(targetCard.OwnerPlayerId);
            if (ownerPlayer == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Owner player for card {targetCard.InstanceId} not found." };
            }

            // The entity method handles the state change and generates the factual CardDestroyedEvent.
            return ownerPlayer.DestroyCard(targetCard, sourceCard, Reason);
        }

        public string GetAnnouncement(GameState gameState)
        {
            var targetId = DestroyTarget?.InstanceId;
            var sourceId = DestroySource?.InstanceId;
            if (!targetId.HasValue)
            {
                return "destroy";
            }

            var targetToken = gameState.InGameIds.Format(InGameIdKind.Card, targetId.Value);
            return sourceId.HasValue
                ? $"destroy target={targetToken} source={gameState.InGameIds.Format(InGameIdKind.Card, sourceId.Value)}"
                : $"destroy target={targetToken}";
        }

        private static bool IsAbilityDestroyReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            return reason.Equals("Effect", StringComparison.OrdinalIgnoreCase) ||
                   reason.StartsWith("Effect", StringComparison.OrdinalIgnoreCase) ||
                   reason.StartsWith("Ability", StringComparison.OrdinalIgnoreCase);
        }
    }
}
