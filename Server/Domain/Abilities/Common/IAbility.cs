using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Constants;
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
        /// Gets the configured keywords for this ability.
        /// </summary>
        IReadOnlyList<AbilityKeywordConfig> AbilityKeywords { get; }

        /// <summary>
        /// Gets the primary keyword for display or legacy fallbacks.
        /// </summary>
        AbilityKeyword PrimaryKeyword { get; }

        /// <summary>
        /// Gets a human-readable keyword summary for logging or UI fallback.
        /// </summary>
        string KeywordSummary { get; }

        /// <summary>
        /// Checks whether this ability includes a specific keyword.
        /// </summary>
        bool HasKeyword(AbilityKeyword keyword);

        /// <summary>
        /// Gets the numeric value attached to a keyword, if any.
        /// </summary>
        int? GetKeywordValue(AbilityKeyword keyword);

        /// <summary>
        /// Gets the type of the ability (e.g., Spell, FollowerEffect, System).
        /// </summary>
        AbilityType Type { get; }

        /// <summary>
        /// Gets the configuration for how this ability is triggered.
        /// </summary>
        TriggerConfig TriggerConfig { get; }

        /// <summary>
        /// Gets the ID of the card instance that is the source of this ability, if applicable.
        /// Null if the ability is not sourced from a specific card (e.g., system abilities).
        /// </summary>
        Guid? SourceCardId { get; }

        /// <summary>
        /// Gets the card instance that is the source of this ability by looking it up in the current game context.
        /// This is a computed property and may be null if the card is not found or if SourceCardId is null.
        /// </summary>
        CardInstance? SourceCard { get; }
        /// <summary>
        /// Gets the ID of the buff instance that is the source of this ability, if applicable.
        /// Null if the ability is not granted by a buff.
        /// </summary>
        Guid? SourceBuffId { get; }

        /// <summary>
        /// Gets the number of times this ability has been triggered during the current turn.
        /// </summary>
        int TriggerCountThisTurn { get; }

        /// <summary>
        /// Gets the total number of times this ability has been triggered during the current game.
        /// </summary>
        int TriggerCountThisGame { get; }

        /// <summary>
        /// Gets the runtime list of targets associated with this ability.
        /// </summary>
        List<Target> Targets { get; }

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
        /// <returns>True if conditions are met; otherwise, false.</returns>
        bool CheckConditions(GameContext context);

        /// <summary>
        /// Resolves all targets for this ability, handling dependencies and player choices atomically.
        /// </summary>
        /// <param name="context">The current game context.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a
        /// <see cref="TargetResolutionResult"/>, which indicates success, contains all resolved
        /// targets, and specifies if further player interaction is required.
        /// </returns>
        /// <param name="isCancellable">Indicates if the player choice initiated by this resolution can be cancelled.</param>
        Task<TargetResolutionResult> ResolveTargetsAsync(GameContext context, bool isCancellable = false);

        /// <summary>
        /// Performs a non-blocking check to determine if this ability has valid targets available.
        /// </summary>
        /// <param name="context">The current game context.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains availability info.</returns>
        Task<TargetAvailabilityResult> CheckTargetAvailabilityAsync(GameContext context);

        /// <summary>
        /// Creates the list of effects that this ability will produce, using the completed target resolution result.
        /// This method acts as an "Effect Factory".
        /// </summary>
        /// <param name="context">The current game context.</param>
        /// <param name="targetResult">The full result from the target resolution phase, containing all named target lists.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of effects to be queued.</returns>
        Task<List<Effect>> CreateEffectsAsync(GameContext context, TargetResolutionResult targetResult);

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
