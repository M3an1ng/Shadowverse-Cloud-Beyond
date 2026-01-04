using System;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // DomainOperationResult
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums; // CardZone
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// 原子事实事件：当一张卡牌在不同区域之间移动时触发。
    /// </summary>
    [GameEventMetadata("移动", "当一张卡牌在不同区域之间移动时触发的原子事件。", EventType.CardMoved, visibility: EventMetadataVisibility.System)]
    public class CardMovedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("起始区域", "卡牌移动前所在的区域。")]
        public CardZone FromZone { get; }

        [GameEventParameter("目标区域", "卡牌移动后所在的区域。")]
        public CardZone ToZone { get; }

        [GameEventParameter("移动的卡牌", "被移动的卡牌。")]
        public CardInstance? MovedCard => Source?.Card;

        [GameEventParameter("移动原因", "卡牌移动到目标区域的原因。")]
        public CardMoveReason MoveReason { get; }

        public CardMovedEvent(CardInstance card, CardZone fromZone, CardZone toZone, CardMoveReason moveReason = CardMoveReason.Unknown)
            : base(EventType.CardMoved)
        {
            FromZone = fromZone;
            ToZone = toZone;
            MoveReason = moveReason;
            Source = TargetInfo.ForCard(card);
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();
            var card = MovedCard;
            if (card == null)
            {
                result.Success = false;
                result.ErrorMessage = "CardMovedEvent.Commit: Source card is null.";
                return result;
            }
            var owner = context.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
            if (owner == null)
            {
                result.Success = false;
                result.ErrorMessage = $"CardMovedEvent.Commit: Owner player {card.OwnerPlayerId} not found.";
                return result;
            }

            // Execute internal state mutation on the aggregate
            bool ok = owner.ApplyCardZoneChange(card, FromZone, ToZone, null);
            if (!ok)
            {
                result.Success = false;
                result.ErrorMessage = $"Failed to apply card move from {FromZone} to {ToZone} for {card.InstanceId}.";
                return result;
            }

            if (!ShouldPreserveState(FromZone, ToZone))
            {
                var resetResult = ResetCardState(context, card);
                if (!resetResult.Success)
                {
                    return resetResult;
                }

                result.GeneratedEvents.AddRange(resetResult.GeneratedEvents);
            }

            card.SetLastMoveReason(MoveReason);

            if (ToZone == CardZone.Field && card.HasKeyword(CardKeyword.EarthSigil))
            {
                var earthSigilResult = ApplyEarthSigilRules(owner, card);
                if (!earthSigilResult.Success)
                {
                    return earthSigilResult;
                }
                result.GeneratedEvents.AddRange(earthSigilResult.GeneratedEvents);
            }

            if (ToZone == CardZone.Graveyard && FromZone != CardZone.Graveyard)
            {
                owner.GraveyardCount = Math.Max(0, owner.GraveyardCount + 1);
            }

            // Derive follow-up notifications (can later be centralized)
            if (ToZone == CardZone.Hand || FromZone == CardZone.Hand)
                result.GeneratedEvents.Add(new HandUpdatedEvent(owner, owner.Hand.ToList()));
            if (ToZone == CardZone.Deck || FromZone == CardZone.Deck)
                result.GeneratedEvents.Add(new DeckCountChangedEvent(owner, owner.Deck.Count));
            if (ToZone == CardZone.Graveyard || FromZone == CardZone.Graveyard)
                result.GeneratedEvents.Add(new GraveyardUpdatedEvent(owner, owner.GraveyardCount));

            result.Success = true;
            return result;
        }

        private static DomainOperationResult ApplyEarthSigilRules(PlayerState owner, CardInstance enteringCard)
        {
            var result = new DomainOperationResult { Success = true };
            var earthSigils = owner.Field
                .Where(card => card.HasKeyword(CardKeyword.EarthSigil))
                .ToList();

            if (earthSigils.Count == 0)
            {
                return result;
            }

            if (enteringCard.GetCounter(CardInstance.EarthsigilCounterKey) == 0)
            {
                enteringCard.SetCounter(CardInstance.EarthsigilCounterKey, 1);
            }

            if (earthSigils.Count == 1)
            {
                return result;
            }

            var total = earthSigils.Sum(card => card.GetCounter(CardInstance.EarthsigilCounterKey));
            enteringCard.SetCounter(CardInstance.EarthsigilCounterKey, total);

            foreach (var other in earthSigils.Where(card => card.InstanceId != enteringCard.InstanceId))
            {
                result.GeneratedEvents.Add(new CardVanishedEvent(other, other.CurrentZone, "EarthSigilMerge"));
            }

            return result;
        }

        private static bool ShouldPreserveState(CardZone from, CardZone to)
        {
            return (from == CardZone.Deck && to == CardZone.Hand)
                || (from == CardZone.Hand && to == CardZone.Deck)
                || (from == CardZone.Hand && to == CardZone.Playing)
                || (from == CardZone.Playing && to == CardZone.Field)
                || (from == CardZone.Deck && to == CardZone.Field);
        }

        private static DomainOperationResult ResetCardState(GameContext context, CardInstance card)
        {
            var result = new DomainOperationResult { Success = true };

            var removeBuffsResult = context.BuffService.RemoveAllBuffs(card.InstanceId, context)
                ?? new DomainOperationResult { Success = true };
            if (!removeBuffsResult.Success)
            {
                return removeBuffsResult;
            }
            result.GeneratedEvents.AddRange(removeBuffsResult.GeneratedEvents);

            context.AbilityManager.UnregisterCardAbilities(card.InstanceId);

            card.ResetToDefinitionState();

            var dataProvider = GameDataProvider.Instance;
            var abilityConfigs = dataProvider == null
                ? null
                : dataProvider.GetAbilityConfigs(card.BaseDefinitionId);
            if (abilityConfigs != null && abilityConfigs.Any())
            {
                var abilities = context.AbilityManager.CreateAbilitiesForCard(card, abilityConfigs);
                foreach (var ability in abilities)
                {
                    context.CurrentGameState.InGameIds.GetOrCreate(InGameIdKind.Ability, ability.RuntimeId);
                }
            }

            context.BuffService.InvalidateDerivedState(context.CurrentGameState, card.InstanceId);

            return result;
        }
    }
}
