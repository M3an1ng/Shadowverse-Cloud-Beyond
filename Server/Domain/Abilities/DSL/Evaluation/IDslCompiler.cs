using System;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    /// <summary>
    /// Defines a service that compiles DSL expression strings into executable delegates.
    /// This is used for high-performance, repetitive evaluation of DSL, such as in filters and conditions.
    /// </summary>
    public interface IDslCompiler
    {
        /// <summary>
        /// Defines how 'self' is allowed to appear in an expression at runtime.
        /// </summary>
        public enum SelfResolutionMode
        {
            ContextOnly = 0,
            ItemOptional = 1,
            ItemRequired = 2
        }

        /// <summary>
        /// Compiles a DSL expression into a predicate that evaluates against a game context.
        /// Used for conditions that only depend on the global game state.
        /// </summary>
        /// <param name="dslExpression">The DSL string to compile.</param>
        /// <returns>A compiled delegate of type Func<GameContext, bool>.</returns>
        Func<GameContext, bool> CompilePredicate(string dslExpression);

        /// <summary>
        /// Compiles a DSL expression into a predicate that evaluates against a specific item and a game context.
        /// Used for filters in queries where 'self' refers to the item being evaluated.
        /// </summary>
        /// <typeparam name="TItem">The type of the item being evaluated (e.g., CardInstance).</typeparam>
        /// <param name="dslExpression">The DSL string to compile.</param>
        /// <returns>A compiled delegate of type Func<TItem, GameContext, bool>.</returns>
        Func<TItem, GameContext, bool> CompilePredicate<TItem>(string dslExpression);

        /// <summary>
        /// Compiles a DSL expression into a predicate that evaluates against a specific item and a target selection context.
        /// Used for filters in queries that depend on pre-selected targets.
        /// </summary>
        Func<TItem, bool> CompilePredicate<TItem>(string dslExpression, TargetSelectionContext tsc);

        /// <summary>
        /// Compiles a DSL expression into a predicate with optional trace collection.
        /// </summary>
        Func<TItem, IDslTraceCollector?, bool> CompilePredicateWithTrace<TItem>(string dslExpression, TargetSelectionContext tsc);

        /// <summary>
        /// Compiles a DSL expression into a function that returns a strongly-typed value.
        /// </summary>
        /// <typeparam name="TItem">The type of the 'self' context item.</typeparam>
        /// <typeparam name="TResult">The expected return type of the expression.</typeparam>
        /// <param name="dslExpression">The DSL string to compile.</param>
        /// <returns>A compiled delegate of type Func<TItem, GameContext, TResult>.</returns>
        Func<TItem, GameContext, TResult> CompileValue<TItem, TResult>(string dslExpression);

        /// <summary>
        /// Compiles a DSL expression into a function that retrieves a value from a specific item and game context.
        /// Used for sort keys or dynamic value retrieval in queries.
        /// </summary>
        /// <typeparam name="TItem">The type of the item being evaluated.</typeparam>
        /// <param name="dslExpression">The DSL string to compile.</param>
        /// <returns>A compiled delegate of type Func<TItem, GameContext, object>.</returns>
        Func<TItem, GameContext, object> CompileGetter<TItem>(string dslExpression);

        /// <summary>
        /// Compiles a DSL expression into a function that retrieves a value from a specific item and target selection context.
        /// Used for sort keys or dynamic values that depend on pre-selected targets.
        /// </summary>
        Func<TItem, object> CompileGetter<TItem>(string dslExpression, TargetSelectionContext tsc);

        /// <summary>
        /// Compiles a DSL expression into a getter with optional trace collection.
        /// </summary>
        Func<TItem, IDslTraceCollector?, object> CompileGetterWithTrace<TItem>(string dslExpression, TargetSelectionContext tsc);

        /// <summary>
        /// Compiles a predicate delegate using an optional runtime self type.
        /// </summary>
        Func<object, bool> CompilePredicateRuntime(
            string dslExpression,
            TargetSelectionContext tsc,
            Type? selfType,
            SelfResolutionMode selfResolutionMode = SelfResolutionMode.ItemOptional);

        /// <summary>
        /// Compiles a predicate-with-trace delegate using an optional runtime self type.
        /// </summary>
        Func<object, IDslTraceCollector?, bool> CompilePredicateWithTraceRuntime(
            string dslExpression,
            TargetSelectionContext tsc,
            Type? selfType,
            SelfResolutionMode selfResolutionMode = SelfResolutionMode.ItemOptional);

        /// <summary>
        /// Compiles a getter delegate using an optional runtime self type.
        /// </summary>
        Func<object, object?> CompileGetterRuntime(
            string dslExpression,
            TargetSelectionContext tsc,
            Type? selfType,
            SelfResolutionMode selfResolutionMode = SelfResolutionMode.ItemOptional);

        /// <summary>
        /// Compiles a getter-with-trace delegate using an optional runtime self type.
        /// </summary>
        Func<object, IDslTraceCollector?, object?> CompileGetterWithTraceRuntime(
            string dslExpression,
            TargetSelectionContext tsc,
            Type? selfType,
            SelfResolutionMode selfResolutionMode = SelfResolutionMode.ItemOptional);
    }
}
