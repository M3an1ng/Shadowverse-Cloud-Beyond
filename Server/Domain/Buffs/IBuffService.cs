using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;

namespace ShadowverseBeyond.Server.Domain.Buffs
{
    /// <summary>
    /// Service responsible for calculating card stats and keywords considering all active buffs.
    /// This centralizes the logic that was previously in CardInstance.
    /// </summary>
    public interface IBuffService
    {
        /// <summary>
        /// Calculates the effective attack of a card instance.
        /// </summary>
        /// <param name="card">The card instance.</param>
        /// <param name="gameContext">The current game context for condition evaluation.</param>
        /// <returns>The effective attack value.</returns>
        int GetEffectiveAttack(CardInstance card, GameContext gameContext);

        /// <summary>
        /// Calculates the effective maximum health of a card instance.
        /// </summary>
        /// <param name="card">The card instance.</param>
        /// <param name="gameContext">The current game context for condition evaluation.</param>
        /// <returns>The effective maximum health value.</returns>
        int GetEffectiveMaxHealth(CardInstance card, GameContext gameContext);

        /// <summary>
        /// Calculates the effective cost of a card instance.
        /// </summary>
        /// <param name="card">The card instance.</param>
        /// <param name="gameContext">The current game context for condition evaluation.</param>
        /// <returns>The effective cost value.</returns>
        int GetEffectiveCost(CardInstance card, GameContext gameContext);

        /// <summary>
        /// Gets the list of effective keywords for a card instance.
        /// </summary>
        /// <param name="card">The card instance.</param>
        /// <param name="gameContext">The current game context for condition evaluation.</param>
        /// <returns>A list of all effective keywords.</returns>
        List<CardKeyword> GetEffectiveKeywords(CardInstance card, GameContext gameContext);

        /// <summary>
        /// Calculates the effective value of an ability keyword (e.g., Act) for a card instance.
        /// </summary>
        /// <param name="card">The card instance.</param>
        /// <param name="keyword">The ability keyword to evaluate.</param>
        /// <param name="baseValue">The base value before buff modifiers.</param>
        /// <param name="gameContext">The current game context for condition evaluation.</param>
        /// <returns>The effective keyword value after applying buff modifiers.</returns>
        int GetEffectiveAbilityKeywordValue(CardInstance card, AbilityKeyword keyword, int baseValue, GameContext gameContext);

        /// <summary>
        /// Creates and applies a keyword buff to a target card.
        /// </summary>
        /// <param name="context">The current game context.</param>
        /// <param name="target">The card to apply the buff to.</param>
        /// <param name="keywords">The keywords to add.</param>
        /// <param name="durationType">The duration of the buff.</param>
        /// <param name="durationParameter">The parameter for the duration (e.g., number of turns).</param>
        /// <returns>The result of the domain operation.</returns>
        DomainOperationResult ApplyKeywordBuff(
            GameContext context,
            CardInstance target,
            List<CardKeyword> keywords,
            DurationType durationType,
            int? durationParameter,
            string? effectKey = null,
            bool canStack = true);

        /// <summary>
        /// Removes buffs that grant a specific keyword from a target card.
        /// </summary>
        /// <param name="target">The card to remove the buff from.</param>
        /// <param name="keywordToRemove">The keyword to remove.</param>
        /// <returns>The result of the domain operation.</returns>
        DomainOperationResult ApplyRemoveKeywordBuff(
            GameContext context,
            CardInstance target,
            List<CardKeyword> keywordsToRemove,
            DurationType durationType,
            int? durationParameter,
            string? effectKey = null,
            bool canStack = true);

        DomainOperationResult ApplyStatBuff(
            GameContext context,
            CardInstance target,
            List<StatModification> modifications,
            DurationType durationType,
            int? durationParameter,
            string? effectKey = null,
            bool canStack = true);

        DomainOperationResult ApplyTribeBuff(
            GameContext context,
            CardInstance target,
            List<string> tribes,
            DurationType durationType,
            int? durationParameter,
            string? effectKey = null,
            bool canStack = true);

        /// <summary>
        /// Registers a new Buff instance, applying stacking rules and storing it.
        /// This is the primary method for adding a buff to a target.
        /// </summary>
        /// <param name="buff">The Buff instance to register.</param>
        /// <returns>A DomainOperationResult containing generated events (e.g., BuffAppliedEvent, BuffRemovedEvent for replacements).</returns>
        DomainOperationResult ApplyBuffProcess(Buff buff, GameContext context, bool canStack = true);

        /// <summary>
        /// Unregisters a specific Buff instance, typically when it expires or is explicitly removed.
        /// </summary>
        /// <param name="buffId">The unique ID of the Buff to unregister.</param>
        /// <returns>A DomainOperationResult containing a BuffRemovedEvent if successful.</returns>
        DomainOperationResult RemoveBuffProcess(Guid buffId, GameContext context);

        /// <summary>
        /// Removes all Buffs that originated from a specific source ability instance.
        /// Useful when the source card leaves play (e.g., for auras).
        /// </summary>
        /// <param name="sourceAbilityId">The unique ID of the source ability.</param>
        /// <returns>A DomainOperationResult containing all BuffRemovedEvents.</returns>
        DomainOperationResult RemoveBuffsFromSource(long sourceAbilityId, GameContext context);

        /// <summary>
        /// Removes all Buffs currently applied to a specific card instance based on a predicate.
        /// </summary>
        /// <param name="cardInstanceId">The ID of the target card instance.</param>
        /// <param name="predicate">A function to determine which buffs to remove. If null, removes all.</param>
        /// <returns>A DomainOperationResult containing all BuffRemovedEvents.</returns>
        DomainOperationResult RemoveAllBuffs(Guid cardInstanceId, GameContext context, Func<Buff, bool>? predicate = null);

        /// <summary>
        /// Advances the refresh cycle and rebuilds derived-state caches.
        /// </summary>
        void RefreshDerivedStateCache(GameContext context, string reason);

        /// <summary>
        /// Invalidates derived-state cache for a specific card instance.
        /// </summary>
        void InvalidateDerivedState(GameState gameState, Guid cardInstanceId);

        /// <summary>
        /// Collects buff removal events for EndOfTurn expiry.
        /// </summary>
        IReadOnlyList<BuffRemovedEvent> CollectEndOfTurnExpiryEvents(GameContext context);

        /// <summary>
        /// Collects buff removal events for StartOfNextTurn expiry.
        /// </summary>
        IReadOnlyList<BuffRemovedEvent> CollectStartOfTurnExpiryEvents(GameContext context, PlayerState startingPlayer);
    }
}
