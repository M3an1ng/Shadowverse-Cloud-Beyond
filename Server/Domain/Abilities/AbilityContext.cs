using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Entities; // For CardInstance
using ShadowverseBeyond.Server.Domain.Events;   // For IGameEvent
using ShadowverseBeyond.Server.Dtos; // For GameStateDto
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For TargetId

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// Holds context specific to the execution of a card ability.
    /// Includes information about the triggering event, the source card,
    /// selected targets, and any dynamic variables associated with the ability's state.
    /// </summary>
    public class AbilityContext
    {
        /// <summary>
        /// The game event that triggered this ability execution. Can be null if not triggered by an event.
        /// </summary>
        public IGameEvent? TriggeringEvent { get; set; }

        /// <summary>
        /// The card instance that owns the ability being executed.
        /// </summary>
        public CardInstance? SourceCard { get; set; }

        /// <summary>
        /// A dictionary to store dynamic variables related to the ability's state or calculations.
        /// Keys are variable names (string), values are the variable data (object).
        /// </summary>
        public Dictionary<string, object> Variables { get; set; } = new();

        /// <summary>
        /// A dictionary storing the results of target selection steps.
        /// Keys are target identifiers (string, e.g., "target_1"), values are lists of selected objects (e.g., List<CardInstanceDto>).
        /// </summary>
        public Dictionary<string, List<object>> Targets { get; set; } = new();

        /// <summary>
        /// The ID of the player whose action triggered the ability or on whose behalf the ability is executing.
        /// </summary>
        public int RequestingPlayerId { get; set; } // Added

        /// <summary>
        /// A snapshot of the game state at the time the ability context was created.
        /// Use DTO to avoid unintended modifications to the actual game state.
        /// Can be null if context is created outside a game.
        /// </summary>
        public GameStateDto? CurrentGameState { get; set; } // Added

        /// <summary>
        /// List of selected targets for the current ability execution.
        /// Used by effect handlers to determine which entities to affect.
        /// </summary>
        public List<TargetId> SelectedTargets { get; set; } = new();

        /// <summary>
        /// Parameters for the current ability execution.
        /// Contains resolved values for effect parameters (e.g., damage amounts, heal amounts).
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>
        /// Default constructor for property initialization.
        /// </summary>
        public AbilityContext()
        {
        }

        /// <summary>
        /// Constructor to initialize the essential context.
        /// </summary>
        /// <param name="gameState">Current game state snapshot</param>
        /// <param name="triggeringEvent">Event that triggered this ability (can be null)</param>
        /// <param name="sourceCard">Card instance that owns the ability</param>
        public AbilityContext(GameStateDto? gameState, IGameEvent? triggeringEvent, CardInstance? sourceCard)
        {
            CurrentGameState = gameState;
            TriggeringEvent = triggeringEvent;
            SourceCard = sourceCard;
        }

        // TODO: Add other relevant context if needed, such as:
        // - PlayerState performing the action that led to the trigger
        // - The specific AbilityConfig being executed
    }
}