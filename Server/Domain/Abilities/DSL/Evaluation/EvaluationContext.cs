using System;
using System; // Added System for ArgumentNullException
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities; // Correct namespace for AbilityContext
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Domain.Events; // Added for IGameEvent

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    /// <summary>
    /// Encapsulates the runtime context required for evaluating DSL expressions.
    /// Provides access to game state, player states, the current item being processed,
    /// ability context, and necessary services like the card repository and function library.
    /// </summary>
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
        /// Context specific to the ability being executed, containing trigger info, selected targets, ability variables, etc.
        /// Can be null if evaluation is not part of an ability execution.
        /// </summary>
        public AbilityContext? AbilityContext { get; init; } // TODO: Verify AbilityContext namespace and definition

        /// <summary>
        /// Repository to fetch card definitions. Required.
        /// </summary>
        public ICardRepository CardRepository { get; init; }

        // TODO: Add Function Library/Registry instance or access mechanism
        // public IFunctionRegistry FunctionRegistry { get; init; }

        /// <summary>
        /// Initializes a new instance of the EvaluationContext.
        /// </summary>
        public EvaluationContext(
            ICardRepository cardRepository,
            GameStateDto? gameState = null,
            PlayerStateDto? player = null,
            PlayerStateDto? opponent = null,
            object? currentItem = null,
            AbilityContext? abilityContext = null)
        // IFunctionRegistry functionRegistry) // Add when function registry is ready
        {
            CardRepository = cardRepository ?? throw new ArgumentNullException(nameof(cardRepository));
            // FunctionRegistry = functionRegistry ?? throw new ArgumentNullException(nameof(functionRegistry));
            GameState = gameState;
            Player = player;
            Opponent = opponent;
            CurrentItem = currentItem;
            AbilityContext = abilityContext;
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

            // TODO: Implement robust identifier resolution logic as outlined in M14 design doc.
            // 1. Check AbilityContext variables (if context exists and path has 1 segment)
            if (AbilityContext?.Variables.TryGetValue(path.First(), out var variableValue) == true && path.Count == 1)
            {
                return variableValue; // Assuming variables are simple values for now
            }
            if (AbilityContext?.Targets.TryGetValue(path.First(), out var targetValue) == true && path.Count == 1)
            {
                return targetValue; // Return the list of targets directly
            }


            // 2. Resolve root object (player, opponent, gamestate, self/this/current)
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
                // If path starts with self/this/current but rootObject was null (e.g. path was just "self")
                // Handle cases like "self" (return CurrentItem) or "self.property"
                if (!remainingPath.Any()) return CurrentItem; // Path is just "self"
                return ResolvePathSegments(CurrentItem, remainingPath);
            }


            // 3. Check global constants or other context elements? (Less common for identifiers)

            // 4. Not found
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
            // TODO: Implement function lookup using the Function Registry/Library
            Console.WriteLine($"Warning: Function lookup for '{functionName}' not implemented."); // Temporary log
            // Example: return FunctionRegistry.GetFunction(functionName);
            throw new NotImplementedException("Function lookup requires implementation of a function registry.");
        }
    }

    // TODO: Define IFunctionDefinition properly, potentially in a separate file or namespace
    public interface IFunctionDefinition
    {
        string Name { get; }
        bool ValidateArguments(List<object?> args);
        Task<object?> ExecuteAsync(List<object?> args, EvaluationContext context);
    }

    // Removed placeholder AbilityContext and IGameEvent definitions as they exist elsewhere.
}