using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // DomainOperationResult
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums; // CardZone
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Domain.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// 原子事实事件：当一张卡牌在不同区域之间移动时触发。
    /// </summary>
    [GameEventMetadata("移动", "当一张卡牌在不同区域之间移动时触发的原子事件。", EventType.CardMoved, visibility: EventMetadataVisibility.System)]
    public class CardMovedEvent : DelayedEventBase, ICommittableEvent
    {
        [GameEventParameter("起始区域", "卡牌移动前所在的区域。")]
        public CardZone FromZone { get; }

        [GameEventParameter("目标区域", "卡牌移动后所在的区域。")]
        public CardZone ToZone { get; }

        [GameEventParameter("移动的卡牌", "被移动的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? MovedCard => Source?.Card;

        [GameEventParameter("移动原因", "卡牌移动到目标区域的原因。")]
        public CardMoveReason MoveReason { get; }

        public CardMovedEvent(
            CardInstance card,
            CardZone fromZone,
            CardZone toZone,
            CardMoveReason moveReason = CardMoveReason.Unknown,
            DelayConfig? delayConfig = null,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.CardMoved, eventId, timestamp)
        {
            FromZone = fromZone;
            ToZone = toZone;
            MoveReason = moveReason;
            ConfigureDelay(delayConfig ?? DelayConfig.Disabled);
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

            var effectiveToZone = ToZone;
            var effectiveMoveReason = MoveReason;
            if (ToZone == CardZone.Hand && owner.Hand.Count >= GameConstants.Hand.MaxHandSize)
            {
                effectiveToZone = CardZone.Graveyard;
                effectiveMoveReason = CardMoveReason.Burned;
            }

            var logger = context.ServiceProvider.GetService<ILogger<CardMovedEvent>>();
            var preserveState = ShouldPreserveState(FromZone, effectiveToZone);
            logger?.LogInformation("[CardMovedEvent] Card {CardName} ({CardId}) move {FromZone}->{ToZone}, reason {Reason}, preserveState={PreserveState}.",
                card.Name, card.InstanceId, FromZone, effectiveToZone, effectiveMoveReason, preserveState);

            // Execute internal state mutation on the aggregate
            bool ok = owner.ApplyCardZoneChange(card, FromZone, effectiveToZone, null);
            if (!ok)
            {
                result.Success = false;
                result.ErrorMessage = $"Failed to apply card move from {FromZone} to {effectiveToZone} for {card.InstanceId}.";
                return result;
            }

            if (!preserveState)
            {
                logger?.LogInformation("[CardMovedEvent] Resetting card state for {CardName} ({CardId}).", card.Name, card.InstanceId);
                var resetResult = ResetCardState(context, card);
                if (!resetResult.Success)
                {
                    return resetResult;
                }

                result.GeneratedEvents.AddRange(resetResult.GeneratedEvents);
            }
            else
            {
                logger?.LogInformation("[CardMovedEvent] PreserveState=true; skipping ResetCardState for {CardName} ({CardId}).", card.Name, card.InstanceId);
            }

            card.SetLastMoveReason(effectiveMoveReason);

            if (effectiveToZone == CardZone.Field && card.HasKeyword(CardKeyword.EarthSigil))
            {
                var earthSigilResult = ApplyEarthSigilRules(owner, card);
                if (!earthSigilResult.Success)
                {
                    return earthSigilResult;
                }
                result.GeneratedEvents.AddRange(earthSigilResult.GeneratedEvents);
            }

            if (effectiveToZone == CardZone.Graveyard && FromZone != CardZone.Graveyard)
            {
                var oldValue = owner.GraveyardCount;
                var newValue = owner.AddCounter(PlayerState.GraveyardCounterKey, 1);
                result.GeneratedEvents.Add(new CounterChangedEvent(owner, PlayerState.GraveyardCounterKey, oldValue, newValue));
            }

            if (effectiveToZone == CardZone.Graveyard && effectiveMoveReason == CardMoveReason.Burned && FromZone != CardZone.Graveyard)
            {
                result.GeneratedEvents.Add(new CardBurnedEvent(owner, card));
            }

            // Derive follow-up notifications (can later be centralized)
            if (effectiveToZone == CardZone.Hand || FromZone == CardZone.Hand)
                result.GeneratedEvents.Add(new HandUpdatedEvent(owner, owner.Hand.ToList()));
            if (effectiveToZone == CardZone.Deck || FromZone == CardZone.Deck)
                result.GeneratedEvents.Add(new DeckCountChangedEvent(owner, owner.Deck.Count));
            if (effectiveToZone == CardZone.Graveyard || FromZone == CardZone.Graveyard)
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
                || (from == CardZone.Staging && to == CardZone.Hand)
                || (from == CardZone.Hand && to == CardZone.Field)
                || (from == CardZone.Hand && to == CardZone.Playing)
                || (from == CardZone.Staging && to == CardZone.Field)
                || (from == CardZone.Staging && to == CardZone.Deck)
                || (from == CardZone.Staging && to == CardZone.Emblem)
                || (from == CardZone.Staging && to == CardZone.Graveyard)
                || (from == CardZone.Staging && to == CardZone.Extra)
                || (from == CardZone.Deck && to == CardZone.Field);
        }

        private static DomainOperationResult ResetCardState(GameContext context, CardInstance card)
        {
            var result = new DomainOperationResult { Success = true };
            var logger = context.ServiceProvider.GetService<ILogger<CardMovedEvent>>();

            // Capture lightweight snapshot before any reset-side effects.
            //card.CapturePreResetSnapshot();

            var abilitiesBeforeReset = context.AbilityManager.GetAbilitiesForCard(card.InstanceId)
                ?? new List<IAbility>();
            logger?.LogInformation("[CardMovedEvent] Pre-reset abilities for {CardName} ({CardId}): {AbilityCount} [{AbilityNames}].",
                card.Name, card.InstanceId, abilitiesBeforeReset.Count, string.Join(", ", abilitiesBeforeReset.Select(a => a.Name)));

            var removeBuffsResult = context.BuffService.RemoveAllBuffs(card.InstanceId, context)
                ?? new DomainOperationResult { Success = true };
            if (!removeBuffsResult.Success)
            {
                return removeBuffsResult;
            }
            result.GeneratedEvents.AddRange(removeBuffsResult.GeneratedEvents);
            logger?.LogInformation("[CardMovedEvent] Removed buffs for {CardName} ({CardId}). GeneratedEvents={EventCount}.",
                card.Name, card.InstanceId, removeBuffsResult.GeneratedEvents.Count);

            logger?.LogInformation("[CardMovedEvent] Unregistering auras for {CardName} ({CardId}).", card.Name, card.InstanceId);
            var auraResult = context.AuraManager.UnregisterBySourceCard(card.InstanceId, context);
            if (!auraResult.Success)
            {
                return auraResult;
            }
            result.GeneratedEvents.AddRange(auraResult.GeneratedEvents);

            // Unregister abilities after aura removal so buff removal can resolve source ability/card.
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

                logger?.LogInformation("[CardMovedEvent] Recreated abilities for {CardName} ({CardId}): {AbilityCount} (configs={ConfigCount}).",
                    card.Name, card.InstanceId, abilities.Count, abilityConfigs.Count);
            }
            else
            {
                logger?.LogInformation("[CardMovedEvent] No ability configs found for {CardName} ({CardId}); abilities not recreated.", card.Name, card.InstanceId);
            }

            context.BuffService.InvalidateDerivedState(context.CurrentGameState, card.InstanceId);

            return result;
        }
    }
}
