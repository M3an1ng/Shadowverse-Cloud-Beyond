using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// Manages the lifecycle and execution of all ability instances within a game.
    /// This includes creating, registering, unregistering, and retrieving abilities.
    /// It also handles the logic for triggering abilities in response to game events.
    /// </summary>
    public interface IAbilityManager
    {
        // --- Ability Lifecycle Management ---

        /// <summary>
        /// Creates and registers all abilities for a given card instance based on its configurations.
        /// </summary>
        List<Ability> CreateAbilitiesForCard(CardInstance cardInstance, List<AbilityConfig> abilityConfigs, Guid? sourceBuffId = null);


        /// <summary>
        /// Registers an ability instance with the manager, making it active and indexed.
        /// </summary>
        void RegisterAbility(IAbility ability);

        /// <summary>
        /// Unregisters an ability instance, removing it from all indexes.
        /// </summary>
        void UnregisterAbility(IAbility ability);

        /// <summary>
        /// Unregisters all abilities associated with a specific card instance ID.
        /// </summary>
        void UnregisterCardAbilities(Guid cardInstanceId);

        /// <summary>
        /// Unregisters all abilities that were granted by a specific buff.
        /// </summary>
        /// <param name="buffId">The ID of the source buff.</param>
        void UnregisterAbilitiesFromBuff(Guid buffId);

        /// <summary>
        /// Gets the runtime ability IDs associated with a buff.
        /// </summary>
        IReadOnlyList<Guid> GetAbilityRuntimeIdsForBuff(Guid buffId);
        /// <summary>
        /// Clears all registered abilities from the manager.
        /// </summary>
        void ClearAllAbilities();

        // --- Ability Retrieval ---

        /// <summary>
        /// Gets all ability instances associated with a specific card instance ID.
        /// </summary>
        List<IAbility> GetAbilitiesForCard(Guid cardInstanceId);

        /// <summary>
        /// Gets all effective (non-suppressed) abilities associated with a specific card instance ID.
        /// </summary>
        List<IAbility> GetEffectiveAbilitiesForCard(Guid cardInstanceId, GameContext context);

        /// <summary>
        /// Determines whether a specific ability is suppressed in the current context.
        /// </summary>
        bool IsAbilitySuppressed(IAbility ability, GameContext context);

        /// <summary>
        /// Gets a specific ability instance by its unique runtime ID.
        /// </summary>
        IAbility? GetAbilityByRuntimeId(Guid runtimeId);

        /// <summary>
        /// Attempts to get a tombstone snapshot for an ability that may have been unregistered.
        /// </summary>
        bool TryGetAbilitySnapshot(Guid runtimeId, out AbilitySnapshot snapshot);

        /// <summary>
        /// Clears all stored ability tombstone snapshots for the current transaction.
        /// </summary>
        void ClearAbilityTombstones();

        /// <summary>
        /// Gets a registered system ability instance by its type.
        /// </summary>
        T? GetSystemAbility<T>() where T : class, IAbility;

        // --- Ability Triggering and Processing ---

        /// <summary>
        /// Gets a list of abilities that are triggered by a specific game event for a given timing.
        /// </summary>
        /// <param name="gameEvent">The game event that could trigger abilities.</param>
        /// <param name="context">The current game context.</param>
        /// <param name="timing">The specific trigger timing (e.g., Before, On, After) to check for.</param>
        /// <returns>A sorted list of abilities that are triggered by the event for the specified timing.</returns>
        Task<List<IAbility>> GetTriggeredAbilitiesAsync(IGameEvent gameEvent, GameContext context, TriggerTiming timing);

        /// <summary>
        /// Processes a single ability instance to generate its effects,
        /// including condition checks and target resolution.
        /// </summary>
        /// <param name="isCancellable">Indicates if any player choice prompted by this ability can be cancelled.</param>
        Task<AbilityProcessResult> ProcessSingleAbilityAsync(IAbility ability, GameContext context, bool isCancellable, bool deferTargetResolution, bool recordTrigger = true);

        /// <summary>
        /// Refreshes aura-only abilities for the given ability instance.
        /// </summary>
        DomainOperationResult RefreshAuraForAbility(IAbility ability, GameContext context);
    }
}
