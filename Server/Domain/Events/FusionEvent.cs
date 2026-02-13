using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// A committable event that represents a card executing Fusion and consuming materials.
    /// </summary>
    [GameEventMetadata(
        "融合时",
        "当融合效果结算并消耗素材时触发的事件。",
        EventType.CardFused,
        AbilityEditorCategories.BaseActionId,
        AbilityEditorCategories.BaseActionLabel)]
    public sealed class FusionEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("融合素材", "本次融合消耗的素材卡牌列表。", DslDisplayTypeName = "事件目标")]
        public IReadOnlyList<CardInstance> FusedCards => Target.Select(t => t.Card).Where(c => c != null).Cast<CardInstance>().ToList();

        [GameEventParameter("融合卡牌", "执行融合动作的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? FusionCard => Source?.Card;

        public FusionEvent(
            CardInstance sourceCard,
            IReadOnlyList<CardInstance> fusedCards,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.CardFused, eventId, timestamp)
        {
            Source = TargetInfo.ForCard(sourceCard);
            if (fusedCards != null)
            {
                foreach (var fusedCard in fusedCards)
                {
                    Target.Add(TargetInfo.ForCard(fusedCard));
                }
            }
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var sourceCard = FusionCard;
            if (sourceCard == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "FusionEvent.Commit: source card is null." };
            }

            if (context.CurrentGameState.GetPlayerState(sourceCard.OwnerPlayerId) == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"FusionEvent.Commit: owner {sourceCard.OwnerPlayerId} not found." };
            }

            var fusedCards = FusedCards;
            if (fusedCards.Count == 0)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "FusionEvent.Commit: no fusion materials provided." };
            }

            var result = new DomainOperationResult { Success = true };
            var seen = new HashSet<Guid>();
            foreach (var fusedCard in fusedCards)
            {
                if (fusedCard == null)
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = "FusionEvent.Commit: fused card is null." };
                }

                if (!seen.Add(fusedCard.InstanceId))
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = "FusionEvent.Commit: duplicate fusion materials." };
                }

                if (fusedCard.OwnerPlayerId != sourceCard.OwnerPlayerId)
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = $"FusionEvent.Commit: fused card {fusedCard.InstanceId} not owned by source player." };
                }
            }

            sourceCard.AddFusedCards(fusedCards);

            foreach (var fusedCard in fusedCards)
            {
                result.GeneratedEvents.Add(new CardVanishedEvent(fusedCard, fusedCard.CurrentZone, "Fusion"));
            }

            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var source = FusionCard;
            var count = FusedCards.Count;
            if (source == null || count <= 0)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Card(source)}融合了 {AnnouncementTokens.Number(count)} 张牌。";
        }
    }
}
