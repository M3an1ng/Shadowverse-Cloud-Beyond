using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Abilities.Common
{
    /// <summary>
    /// Represents a runtime executable ability, which can be a card ability or a system ability.
    /// This interface defines the contract for all abilities that can be triggered and resolved
    /// within the game loop.
    /// </summary>
    public interface IAbility
    {
        /// <summary>
        /// Gets the unique runtime identifier for this instance of the ability.
        /// </summary>
        Guid RuntimeId { get; }

        /// <summary>
        /// Gets the definition ID of the ability, linking to its static configuration.
        /// </summary>
        long DefinitionId { get; }

        /// <summary>
        /// Gets the name of the ability.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets a tag or category for the ability, used for grouping or specific interactions.
        /// </summary>
        string AbilityTag { get; }

        /// <summary>
        /// Gets the type of the ability (e.g., Spell, FollowerEffect, System).
        /// </summary>
        AbilityType Type { get; }

        /// <summary>
        /// Gets the configuration for how this ability is triggered.
        /// </summary>
        TriggerConfig TriggerConfig { get; }

        /// <summary>
        /// Gets or sets the card instance that is the source of this ability, if applicable.
        /// Null if the ability is not sourced from a specific card (e.g., system abilities).
        /// </summary>
        CardInstance? SourceCard { get; set; }

        /// <summary>
        /// Checks if the ability's trigger conditions are met by the given game event.
        /// </summary>
        /// <param name="gameEvent">The game event to check against.</param>
        /// <param name="context">The current game context.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if the ability can be triggered by the event, false otherwise.</returns>
        Task<bool> CanTrigger(IGameEvent gameEvent, GameContext context);

        /// <summary>
        /// Checks if the conditions for this ability to trigger or resolve are met.
        /// </summary>
        /// <param name="context">The current game context.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains true if conditions are met; otherwise, false.</returns>
        Task<bool> CheckConditionsAsync(GameContext context);

        /// <summary>
        /// Resolves the targets for this ability based on its target configuration and the game state.
        /// This method is responsible for all target-finding logic.
        /// </summary>
        /// <param name="context">The current game context, providing access to the game state.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a list
        /// of <see cref="TargetInfo"/> objects representing the resolved targets.
        /// </returns>
        Task<List<TargetInfo>> ResolveTargetsAsync(GameContext context);

        /// <summary>
        /// Creates the list of effects that this ability will produce, using the pre-resolved targets.
        /// This method acts as an "Effect Factory".
        /// </summary>
        /// <param name="context">The current game context.</param>
        /// <param name="resolvedTargets">The list of targets that have already been resolved by <see cref="ResolveTargetsAsync"/>.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of effects to be queued.</returns>
        Task<List<Effect>> CreateEffectsAsync(GameContext context, List<TargetInfo> resolvedTargets);

        /// <summary>
        /// Records that this ability has been triggered.
        /// </summary>
        /// <param name="context">The current game context.</param>
        void RecordTrigger(GameContext context);

        /// <summary>
        /// Gets the number of times this ability has been triggered during the current turn.
        /// </summary>
        /// <param name="context">The current game context.</param>
        /// <returns>The trigger count for the current turn.</returns>
        int GetTriggerCountThisTurn(GameContext context);

        /// <summary>
        /// Gets the total number of times this ability has been triggered during the current game.
        /// </summary>
        /// <param name="context">The current game context.</param>
        /// <returns>The trigger count for the current game.</returns>
        int GetTriggerCountThisGame(GameContext context);

        /// <summary>
        /// Resets any turn-specific state for this ability, such as the turn trigger count.
        /// </summary>
        /// <param name="context">The current game context.</param>
        void ResetTurnState(GameContext context);

    }

    // Placeholder for AbilityType and AbilityState enums if they are not defined elsewhere
    // Or ensure they are correctly referenced via 'using' statements if defined in other namespaces.
    // Example:
    // public enum AbilityType { Spell, FollowerEffect, System, Aura, LastWords, Fanfare }
    // public enum AbilityState { Inactive, PendingActivation, Active, PendingResolution, Resolving, Resolved, Suspended, Destroyed }
}