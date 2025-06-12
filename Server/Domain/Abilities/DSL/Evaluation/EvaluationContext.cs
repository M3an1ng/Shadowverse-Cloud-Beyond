using System;
using System; // Added System for ArgumentNullException
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Entities; // Added for CardInstance
using ShadowverseBeyond.Server.Domain.Abilities; // Correct namespace for AbilityContext
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Domain.Events; // Added for IGameEvent
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    /// <summary>
    /// Encapsulates the runtime context required for evaluating DSL expressions.
    /// Provides access to game state, player states, the current item being processed,
    /// ability context, and necessary services like the card repository and function library.
    ///
    /// 注意：此类已被GameContext替代，仅作为向后兼容的桥接层保留。
    /// </summary>
    [Obsolete("Use GameContext instead. This class will be removed in a future version.")]
    public class EvaluationContext
    {
        /// <summary>
        /// The current state of the game. Can be null if evaluation happens outside a game context.
        /// </summary>
        public GameStateDto? GameState { get; init; }

        /// <summary>
        /// The state of the player from whose perspective the expression is evaluated (often the controller of the ability/card).
        /// </summary>
        public PlayerStateDto? Player { get; init; }

        /// <summary>
        /// The state of the opponent player.
        /// </summary>
        public PlayerStateDto? Opponent { get; init; }

        /// <summary>
        /// The primary object the expression relates to, often the card instance or definition triggering the evaluation.
        /// Can be CardInstanceDto, CardDefinitionDto, or potentially other types depending on the evaluation trigger.
        /// Aliased as 'self', 'this', 'current' in DSL identifiers.
        /// </summary>
        public object? CurrentItem { get; init; }

        /// <summary>
        /// Temporary variables for the current evaluation context.
        /// Replaces AbilityContext.TemporaryVariables functionality.
        /// </summary>
        public Dictionary<string, object> TemporaryVariables { get; init; } = new();

        /// <summary>
        /// Repository to fetch card definitions. Required.
        /// </summary>
        public ICardRepository CardRepository { get; init; }

        /// <summary>
        /// Function registry for DSL function lookup. Required.
        /// </summary>
        public IFunctionRegistry FunctionRegistry { get; init; }

        /// <summary>
        /// Initializes a new instance of the EvaluationContext.
        /// </summary>
        public EvaluationContext(
            ICardRepository cardRepository,
            IFunctionRegistry functionRegistry,
            GameStateDto? gameState = null,
            PlayerStateDto? player = null,
            PlayerStateDto? opponent = null,
            object? currentItem = null,
            Dictionary<string, object>? temporaryVariables = null)
        {
            CardRepository = cardRepository ?? throw new ArgumentNullException(nameof(cardRepository));
            FunctionRegistry = functionRegistry ?? throw new ArgumentNullException(nameof(functionRegistry));
            GameState = gameState;
            Player = player;
            Opponent = opponent;
            CurrentItem = currentItem;
            TemporaryVariables = temporaryVariables ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// Resolves an identifier path (e.g., "player.hand.count", "self.attack", "target_1")
        /// against the current context.
        /// </summary>
        /// <param name="path">A list of strings representing the identifier segments.</param>
        /// <returns>The resolved value, or null if not found or the path is invalid.</returns>
        public async Task<object?> ResolveIdentifierAsync(List<string> path)
        {
            if (path == null || !path.Any()) return null;

            // 1. Check temporary variables (if path has 1 segment)
            if (TemporaryVariables.TryGetValue(path.First(), out var tempVariableValue) && path.Count == 1)
            {
                return tempVariableValue;
            }

            // 2. Target resolution is now handled by GameContext, not needed here

            // 3. Check CardInstance persistent state (e.g., self.state.counter)
            if (path.Count > 2 &&
                (path[0].ToLowerInvariant() is "self" or "this" or "current") &&
                path[1].ToLowerInvariant() == "state" &&
                CurrentItem is CardInstance sourceCard)
            {
                string variableName = path[2]; // Assuming path is like ["self", "state", "variableName"]
                if (sourceCard.AbilityState.TryGetValue(variableName, out var stateValue))
                {
                    // TODO: Handle further path segments if needed (e.g., self.state.complexObject.property)
                    if (path.Count == 3)
                    {
                        return stateValue;
                    }
                    else
                    {
                        // Resolve remaining path segments against the state value
                        return ResolvePathSegments(stateValue, path.Skip(3).ToList());
                    }
                }
                else
                {
                    // Variable not found in AbilityState, continue to other resolution paths
                    // Or return failure? Decide on behavior. For now, continue.
                }
            }


            // 4. Resolve root object (player, opponent, gamestate, self/this/current)
            object? rootObject = path.First().ToLowerInvariant() switch
            {
                "player" => Player,
                "opponent" => Opponent,
                "gamestate" => GameState,
                "self" or "this" or "current" => CurrentItem,
                // TODO: Add "event" if TriggeringEvent is added to context
                _ => null
            };

            List<string> remainingPath = path.Skip(1).ToList();

            if (rootObject != null)
            {
                // Use helper to resolve path segments against the root object
                return ResolvePathSegments(rootObject, remainingPath);
            }
            else if (path.First().ToLowerInvariant() is "self" or "this" or "current" && CurrentItem != null)
            {
                // Handle cases like "self" (return CurrentItem) or "self.property" (excluding self.state.* handled above)
                if (!remainingPath.Any()) return CurrentItem; // Path is just "self"

                // Avoid re-resolving self.state.* here
                if (remainingPath.Count > 0 && remainingPath[0].ToLowerInvariant() == "state")
                {
                    // Already handled or variable not found in state, fall through to not found
                }
                else
                {
                    return ResolvePathSegments(CurrentItem, remainingPath);
                }
            }


            // 5. Check global constants or other context elements? (Less common for identifiers)

            // 6. Not found
            // Consider logging a warning here
            Console.WriteLine($"Warning: Identifier not found or path invalid: {string.Join(".", path)}"); // Temporary console log
            return null;
        }

        /// <summary>
        /// Recursively resolves property segments against a target object.
        /// </summary>
        private object? ResolvePathSegments(object? target, List<string> segments)
        {
            object? currentTarget = target;
            foreach (var segment in segments)
            {
                if (currentTarget == null) return null; // Cannot resolve further

                // Handle special case: List.Count
                if (segment.Equals("Count", StringComparison.OrdinalIgnoreCase) && currentTarget is System.Collections.ICollection collection)
                {
                    currentTarget = collection.Count;
                    continue;
                }
                // Handle special case: List.IsEmpty (assuming extension method or property)
                if (segment.Equals("IsEmpty", StringComparison.OrdinalIgnoreCase) && currentTarget is System.Collections.ICollection collectionIsEmpty)
                {
                    currentTarget = collectionIsEmpty.Count == 0;
                    continue;
                }


                var propertyInfo = currentTarget.GetType().GetProperty(segment, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (propertyInfo == null)
                {
                    Console.WriteLine($"Warning: Property '{segment}' not found on type '{currentTarget.GetType().Name}'"); // Temporary log
                    return null; // Property not found
                }
                currentTarget = propertyInfo.GetValue(currentTarget);
            }
            return currentTarget;
        }


        /// <summary>
        /// Finds a function definition by name from the registered function library.
        /// </summary>
        /// <param name="functionName">The name of the function.</param>
        /// <returns>The function definition, or null if not found.</returns>
        public IFunctionDefinition? FindFunction(string functionName)
        {
            if (string.IsNullOrWhiteSpace(functionName))
            {
                return null;
            }

            return FunctionRegistry.GetFunction(functionName);
        }
    }
}