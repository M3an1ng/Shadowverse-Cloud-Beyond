using System;
using System.Collections.Concurrent;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.Events;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    public class DslCompiler : IDslCompiler
    {
        private readonly ILogger<DslCompiler> _logger;
        private readonly IFunctionRegistry _functionRegistry;
        private readonly IDslExpressionCache _cache;
        private readonly IGameDataProvider _gameDataProvider;

        public DslCompiler(
            ILogger<DslCompiler> logger,
            IFunctionRegistry functionRegistry,
            IDslExpressionCache cache,
            IGameDataProvider gameDataProvider)
        {
            _logger = logger;
            _functionRegistry = functionRegistry;
            _cache = cache;
            _gameDataProvider = gameDataProvider;
        }

        public Func<GameContext, bool> CompilePredicate(string dslExpression)
        {
            if (string.IsNullOrWhiteSpace(dslExpression))
                return (ctx) => true;

            if (_cache.TryGetValue<Func<GameContext, bool>>(dslExpression, out var cachedPredicate))
            {
                return cachedPredicate!;
            }

            _logger.LogDebug("Compiling GameContext Predicate: {Dsl}", dslExpression);

            var lexer = new Lexer(dslExpression);
            var tokens = lexer.Tokenize();
            var parser = new ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser.Parser(tokens);
            var ast = parser.ParseSingleExpression();
            if (ast == null)
            {
                _logger.LogError("Failed to parse DSL expression for GameContext: {Dsl}", dslExpression);
                throw new InvalidOperationException($"Failed to parse DSL expression: {dslExpression}");
            }

            var gameContextParam = Expression.Parameter(typeof(GameContext), "game");
            var dbContextParam = Expression.Parameter(typeof(IGameDataProvider), "db");
            var dummySelfParam = Expression.Parameter(typeof(object), "self");

            var builder = new ExpressionTreeBuilder(dummySelfParam, gameContextParam, dbContextParam, _functionRegistry);
            var body = builder.Build(ast);

            if (body.Type != typeof(bool))
            {
                throw new InvalidOperationException($"DSL expression '{dslExpression}' does not evaluate to a boolean value.");
            }

            var innerLambda = Expression.Lambda<Func<GameContext, IGameDataProvider, bool>>(body, gameContextParam, dbContextParam);
            var compiledInner = innerLambda.Compile();

            // Outer lambda captures the _gameDataProvider instance.
            Func<GameContext, bool> outerLambda = (ctx) => compiledInner(ctx, _gameDataProvider);

            _cache.Set(dslExpression, outerLambda);

            return outerLambda;
        }

        public Func<TItem, GameContext, bool> CompilePredicate<TItem>(string dslExpression)
        {
            if (string.IsNullOrWhiteSpace(dslExpression))
                return (item, ctx) => true;

            var cacheKey = $"{typeof(TItem).FullName}:{dslExpression}";

            if (_cache.TryGetValue<Func<TItem, GameContext, bool>>(cacheKey, out var cachedPredicate))
            {
                return cachedPredicate!;
            }

            _logger.LogDebug("Compiling Predicate for type {TItem}: {Dsl}", typeof(TItem).Name, dslExpression);

            var lexer = new Lexer(dslExpression);
            var tokens = lexer.Tokenize();
            var parser = new ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser.Parser(tokens);
            var ast = parser.ParseSingleExpression();
            if (ast == null)
            {
                _logger.LogError("Failed to parse DSL expression for {TItem}: {Dsl}", typeof(TItem).Name, dslExpression);
                throw new InvalidOperationException($"Failed to parse DSL expression: {dslExpression}");
            }

            var selfParam = Expression.Parameter(typeof(TItem), "self");
            var gameContextParam = Expression.Parameter(typeof(GameContext), "game");
            var dbContextParam = Expression.Parameter(typeof(IGameDataProvider), "db");

            var builder = new ExpressionTreeBuilder(selfParam, gameContextParam, dbContextParam, _functionRegistry);
            var body = builder.Build(ast);

            if (body.Type != typeof(bool))
            {
                throw new InvalidOperationException($"DSL expression '{dslExpression}' does not evaluate to a boolean value.");
            }

            var innerLambda = Expression.Lambda<Func<TItem, GameContext, IGameDataProvider, bool>>(body, selfParam, gameContextParam, dbContextParam);
            var compiledInner = innerLambda.Compile();

            Func<TItem, GameContext, bool> outerLambda = (item, ctx) => compiledInner(item, ctx, _gameDataProvider);

            _cache.Set(cacheKey, outerLambda);

            return outerLambda;
        }
        public Func<TItem, GameContext, TResult> CompileValue<TItem, TResult>(string dslExpression)
        {
            if (string.IsNullOrWhiteSpace(dslExpression))
            {
                _logger.LogWarning("CompileValue called with empty DSL expression.");
                return (item, ctx) => throw new InvalidOperationException("Cannot execute an empty DSL expression for a value.");
            }

            var cacheKey = $"Value:{typeof(TItem).FullName}:{typeof(TResult).FullName}:{dslExpression}";

            if (_cache.TryGetValue<Func<TItem, GameContext, TResult>>(cacheKey, out var cachedValue))
            {
                return cachedValue!;
            }

            _logger.LogDebug("Compiling Value for type {TItem} -> {TResult}: {Dsl}", typeof(TItem).Name, typeof(TResult).Name, dslExpression);

            var lexer = new Lexer(dslExpression);
            var tokens = lexer.Tokenize();
            var parser = new ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser.Parser(tokens);
            var ast = parser.ParseSingleExpression();
            if (ast == null)
            {
                _logger.LogError("Failed to parse DSL expression for Value<{TItem}, {TResult}>: {Dsl}", typeof(TItem).Name, typeof(TResult).Name, dslExpression);
                throw new InvalidOperationException($"Failed to parse DSL expression: {dslExpression}");
            }

            var selfParam = Expression.Parameter(typeof(TItem), "self");
            var gameContextParam = Expression.Parameter(typeof(GameContext), "game");
            var dbContextParam = Expression.Parameter(typeof(IGameDataProvider), "db");

            var builder = new ExpressionTreeBuilder(selfParam, gameContextParam, dbContextParam, _functionRegistry);
            var body = builder.Build(ast);

            if (!typeof(TResult).IsAssignableFrom(body.Type))
            {
                try
                {
                    body = Expression.Convert(body, typeof(TResult));
                }
                catch (InvalidOperationException)
                {
                    _logger.LogError("DSL expression '{Dsl}' evaluates to type {BodyType}, which cannot be converted to the expected type {TResult}.",
                        dslExpression, body.Type.Name, typeof(TResult).Name);
                    throw new InvalidOperationException($"DSL expression '{dslExpression}' evaluates to type {body.Type.Name}, which is not assignable to {typeof(TResult).Name}.");
                }
            }

            var innerLambda = Expression.Lambda<Func<TItem, GameContext, IGameDataProvider, TResult>>(body, selfParam, gameContextParam, dbContextParam);
            var compiledInner = innerLambda.Compile();

            Func<TItem, GameContext, TResult> outerLambda = (item, ctx) => compiledInner(item, ctx, _gameDataProvider);

            _cache.Set(cacheKey, outerLambda);

            return outerLambda;
        }
        public Func<TItem, GameContext, object> CompileGetter<TItem>(string dslExpression)
        {
            if (string.IsNullOrWhiteSpace(dslExpression))
                return (item, ctx) => throw new InvalidOperationException("Cannot execute an empty DSL expression for a getter.");

            var cacheKey = $"Getter:{typeof(TItem).FullName}:{dslExpression}";

            if (_cache.TryGetValue<Func<TItem, GameContext, object>>(cacheKey, out var cachedGetter))
            {
                return cachedGetter!;
            }

            _logger.LogDebug("Compiling Getter for type {TItem}: {Dsl}", typeof(TItem).Name, dslExpression);

            var lexer = new Lexer(dslExpression);
            var tokens = lexer.Tokenize();
            var parser = new ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser.Parser(tokens);
            var ast = parser.ParseSingleExpression();
            if (ast == null)
            {
                _logger.LogError("Failed to parse DSL expression for Getter {TItem}: {Dsl}", typeof(TItem).Name, dslExpression);
                throw new InvalidOperationException($"Failed to parse DSL expression: {dslExpression}");
            }

            var selfParam = Expression.Parameter(typeof(TItem), "self");
            var gameContextParam = Expression.Parameter(typeof(GameContext), "game");
            var dbContextParam = Expression.Parameter(typeof(IGameDataProvider), "db");

            var builder = new ExpressionTreeBuilder(selfParam, gameContextParam, dbContextParam, _functionRegistry);
            var body = builder.Build(ast);

            if (body.Type.IsValueType)
            {
                body = Expression.Convert(body, typeof(object));
            }

            var innerLambda = Expression.Lambda<Func<TItem, GameContext, IGameDataProvider, object>>(body, selfParam, gameContextParam, dbContextParam);
            var compiledInner = innerLambda.Compile();

            Func<TItem, GameContext, object> outerLambda = (item, ctx) => compiledInner(item, ctx, _gameDataProvider);

            _cache.Set(cacheKey, outerLambda);

            return outerLambda;
        }


        public Func<TItem, bool> CompilePredicate<TItem>(string dslExpression, TargetSelectionContext tsc)
        {
            if (string.IsNullOrWhiteSpace(dslExpression))
                return (item) => true;

            // [STATE_POLLUTION_FIX] Change cache key to store the generic, inner delegate.
            var cacheKey = $"TSC_Predicate_Inner:{typeof(TItem).FullName}:{dslExpression}";

            // [STATE_POLLUTION_FIX] The cached item is now the inner delegate, which does NOT capture the context.
            if (!_cache.TryGetValue<Func<TItem, TargetSelectionContext, GameContext, IGameDataProvider, bool>>(cacheKey, out var compiledInner))
            {
                // Cache miss: Compile the inner, generic delegate.
                var lexer = new Lexer(dslExpression);
                var tokens = lexer.Tokenize();
                var parser = new Parser.Parser(tokens);
                var ast = parser.ParseSingleExpression() ?? throw new InvalidOperationException($"Failed to parse DSL: {dslExpression}");

                var selfParam = Expression.Parameter(typeof(TItem), "self");
                var tscParam = Expression.Parameter(typeof(TargetSelectionContext), "tsc");
                var gameContextParam = Expression.Parameter(typeof(GameContext), "game");
                var dbContextParam = Expression.Parameter(typeof(IGameDataProvider), "db");

                var builder = new ExpressionTreeBuilder(selfParam, gameContextParam, tscParam, dbContextParam, _functionRegistry);
                var body = builder.Build(ast);

                if (body.Type != typeof(bool))
                {
                    throw new InvalidOperationException($"DSL expression '{dslExpression}' does not evaluate to a boolean value.");
                }

                var innerLambda = Expression.Lambda<Func<TItem, TargetSelectionContext, GameContext, IGameDataProvider, bool>>(
                    body,
                    selfParam,
                    tscParam,
                    gameContextParam,
                    dbContextParam);
                compiledInner = innerLambda.Compile();

                // [STATE_POLLUTION_FIX] Store the generic, inner delegate in the cache.
                _cache.Set(cacheKey, compiledInner);
            }

            // [STATE_POLLUTION_FIX] Always return a new outer lambda that captures the CURRENT tsc instance for this specific call.
            // This prevents the cached delegate from holding a reference to a stale context.
            var gameContext = tsc.GameContext;
            Func<TItem, bool> outerLambda = (item) => compiledInner(item, tsc, gameContext, _gameDataProvider);
            return outerLambda;
        }

        public Func<TItem, IDslTraceCollector?, bool> CompilePredicateWithTrace<TItem>(string dslExpression, TargetSelectionContext tsc)
        {
            if (string.IsNullOrWhiteSpace(dslExpression))
                return (item, trace) => true;

            var cacheKey = $"TSC_Predicate_Trace_Inner:{typeof(TItem).FullName}:{dslExpression}";

            if (!_cache.TryGetValue<Func<TItem, TargetSelectionContext, GameContext, IGameDataProvider, IDslTraceCollector?, bool>>(cacheKey, out var compiledInner))
            {
                var lexer = new Lexer(dslExpression);
                var tokens = lexer.Tokenize();
                var parser = new Parser.Parser(tokens);
                var ast = parser.ParseSingleExpression() ?? throw new InvalidOperationException($"Failed to parse DSL: {dslExpression}");

                var selfParam = Expression.Parameter(typeof(TItem), "self");
                var tscParam = Expression.Parameter(typeof(TargetSelectionContext), "tsc");
                var gameContextParam = Expression.Parameter(typeof(GameContext), "game");
                var dbContextParam = Expression.Parameter(typeof(IGameDataProvider), "db");
                var traceParam = Expression.Parameter(typeof(IDslTraceCollector), "trace");

                var builder = new ExpressionTreeBuilder(selfParam, gameContextParam, tscParam, dbContextParam, _functionRegistry, traceParam);
                var body = builder.Build(ast);

                if (body.Type != typeof(bool))
                {
                    throw new InvalidOperationException($"DSL expression '{dslExpression}' does not evaluate to a boolean value.");
                }

                var innerLambda = Expression.Lambda<Func<TItem, TargetSelectionContext, GameContext, IGameDataProvider, IDslTraceCollector?, bool>>(
                    body,
                    selfParam,
                    tscParam,
                    gameContextParam,
                    dbContextParam,
                    traceParam);
                compiledInner = innerLambda.Compile();

                _cache.Set(cacheKey, compiledInner);
            }

            var gameContext = tsc.GameContext;
            Func<TItem, IDslTraceCollector?, bool> outerLambda = (item, trace) => compiledInner(item, tsc, gameContext, _gameDataProvider, trace);
            return outerLambda;
        }

        public Func<TItem, IDslTraceCollector?, object> CompileGetterWithTrace<TItem>(string dslExpression, TargetSelectionContext tsc)
        {
            if (string.IsNullOrWhiteSpace(dslExpression))
                return (item, trace) => throw new InvalidOperationException("Cannot execute an empty DSL expression for a getter.");

            var cacheKey = $"TSC_Getter_Trace_Inner:{typeof(TItem).FullName}:{dslExpression}";

            if (!_cache.TryGetValue<Func<TItem, TargetSelectionContext, GameContext, IGameDataProvider, IDslTraceCollector?, object>>(cacheKey, out var compiledInner))
            {
                var lexer = new Lexer(dslExpression);
                var tokens = lexer.Tokenize();
                var parser = new Parser.Parser(tokens);
                var ast = parser.ParseSingleExpression() ?? throw new InvalidOperationException($"Failed to parse DSL: {dslExpression}");

                var selfParam = Expression.Parameter(typeof(TItem), "self");
                var tscParam = Expression.Parameter(typeof(TargetSelectionContext), "tsc");
                var gameContextParam = Expression.Parameter(typeof(GameContext), "game");
                var dbContextParam = Expression.Parameter(typeof(IGameDataProvider), "db");
                var traceParam = Expression.Parameter(typeof(IDslTraceCollector), "trace");

                var builder = new ExpressionTreeBuilder(selfParam, gameContextParam, tscParam, dbContextParam, _functionRegistry, traceParam);
                var body = builder.Build(ast);

                if (body.Type.IsValueType)
                {
                    body = Expression.Convert(body, typeof(object));
                }

                var innerLambda = Expression.Lambda<Func<TItem, TargetSelectionContext, GameContext, IGameDataProvider, IDslTraceCollector?, object>>(
                    body,
                    selfParam,
                    tscParam,
                    gameContextParam,
                    dbContextParam,
                    traceParam);
                compiledInner = innerLambda.Compile();

                _cache.Set(cacheKey, compiledInner);
            }

            var gameContext = tsc.GameContext;
            Func<TItem, IDslTraceCollector?, object> outerLambda = (item, trace) => compiledInner(item, tsc, gameContext, _gameDataProvider, trace);
            return outerLambda;
        }

        public Func<TItem, object> CompileGetter<TItem>(string dslExpression, TargetSelectionContext tsc)
        {
            if (string.IsNullOrWhiteSpace(dslExpression))
                return (item) => throw new InvalidOperationException("Cannot execute an empty DSL expression for a getter.");

            // [STATE_POLLUTION_FIX] Change cache key to store the generic, inner delegate.
            var cacheKey = $"TSC_Getter_Inner:{typeof(TItem).FullName}:{dslExpression}";

            // [STATE_POLLUTION_FIX] The cached item is now the inner delegate, which does NOT capture the context.
            if (!_cache.TryGetValue<Func<TItem, TargetSelectionContext, GameContext, IGameDataProvider, object>>(cacheKey, out var compiledInner))
            {
                // Cache miss: Compile the inner, generic delegate.
                var lexer = new Lexer(dslExpression);
                var tokens = lexer.Tokenize();
                var parser = new Parser.Parser(tokens);
                var ast = parser.ParseSingleExpression() ?? throw new InvalidOperationException($"Failed to parse DSL: {dslExpression}");

                var selfParam = Expression.Parameter(typeof(TItem), "self");
                var tscParam = Expression.Parameter(typeof(TargetSelectionContext), "tsc");
                var gameContextParam = Expression.Parameter(typeof(GameContext), "game");
                var dbContextParam = Expression.Parameter(typeof(IGameDataProvider), "db");

                var builder = new ExpressionTreeBuilder(selfParam, gameContextParam, tscParam, dbContextParam, _functionRegistry);
                var body = builder.Build(ast);

                if (body.Type.IsValueType)
                {
                    body = Expression.Convert(body, typeof(object));
                }

                var innerLambda = Expression.Lambda<Func<TItem, TargetSelectionContext, GameContext, IGameDataProvider, object>>(
                    body,
                    selfParam,
                    tscParam,
                    gameContextParam,
                    dbContextParam);
                compiledInner = innerLambda.Compile();

                // [STATE_POLLUTION_FIX] Store the generic, inner delegate in the cache.
                _cache.Set(cacheKey, compiledInner);
            }

            // [STATE_POLLUTION_FIX] Always return a new outer lambda that captures the CURRENT tsc instance for this specific call.
            var gameContext = tsc.GameContext;
            Func<TItem, object> outerLambda = (item) => compiledInner(item, tsc, gameContext, _gameDataProvider);
            return outerLambda;
        }

        public Func<object, bool> CompilePredicateRuntime(
            string dslExpression,
            TargetSelectionContext tsc,
            Type? selfType,
            IDslCompiler.SelfResolutionMode selfResolutionMode = IDslCompiler.SelfResolutionMode.ItemOptional)
        {
            EnsureSelfUsageIsValid(dslExpression, selfType, selfResolutionMode);

            if (selfType == null)
            {
                var fallback = CompilePredicate<object>(dslExpression, tsc);
                return item => fallback(item);
            }

            var method = typeof(DslCompiler)
                .GetMethod(nameof(CompileTypedPredicateRuntimeAdapter), global::System.Reflection.BindingFlags.NonPublic | global::System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(selfType);
            return (Func<object, bool>)method.Invoke(this, new object[] { dslExpression, tsc })!;
        }

        public Func<object, IDslTraceCollector?, bool> CompilePredicateWithTraceRuntime(
            string dslExpression,
            TargetSelectionContext tsc,
            Type? selfType,
            IDslCompiler.SelfResolutionMode selfResolutionMode = IDslCompiler.SelfResolutionMode.ItemOptional)
        {
            EnsureSelfUsageIsValid(dslExpression, selfType, selfResolutionMode);

            if (selfType == null)
            {
                var fallback = CompilePredicateWithTrace<object>(dslExpression, tsc);
                return (item, trace) => fallback(item, trace);
            }

            var method = typeof(DslCompiler)
                .GetMethod(nameof(CompileTypedPredicateWithTraceRuntimeAdapter), global::System.Reflection.BindingFlags.NonPublic | global::System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(selfType);
            return (Func<object, IDslTraceCollector?, bool>)method.Invoke(this, new object[] { dslExpression, tsc })!;
        }

        public Func<object, object?> CompileGetterRuntime(
            string dslExpression,
            TargetSelectionContext tsc,
            Type? selfType,
            IDslCompiler.SelfResolutionMode selfResolutionMode = IDslCompiler.SelfResolutionMode.ItemOptional)
        {
            EnsureSelfUsageIsValid(dslExpression, selfType, selfResolutionMode);

            if (selfType == null)
            {
                var fallback = CompileGetter<object>(dslExpression, tsc);
                return item => fallback(item);
            }

            var method = typeof(DslCompiler)
                .GetMethod(nameof(CompileTypedGetterRuntimeAdapter), global::System.Reflection.BindingFlags.NonPublic | global::System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(selfType);
            return (Func<object, object?>)method.Invoke(this, new object[] { dslExpression, tsc })!;
        }

        public Func<object, IDslTraceCollector?, object?> CompileGetterWithTraceRuntime(
            string dslExpression,
            TargetSelectionContext tsc,
            Type? selfType,
            IDslCompiler.SelfResolutionMode selfResolutionMode = IDslCompiler.SelfResolutionMode.ItemOptional)
        {
            EnsureSelfUsageIsValid(dslExpression, selfType, selfResolutionMode);

            if (selfType == null)
            {
                var fallback = CompileGetterWithTrace<object>(dslExpression, tsc);
                return (item, trace) => fallback(item, trace);
            }

            var method = typeof(DslCompiler)
                .GetMethod(nameof(CompileTypedGetterWithTraceRuntimeAdapter), global::System.Reflection.BindingFlags.NonPublic | global::System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(selfType);
            return (Func<object, IDslTraceCollector?, object?>)method.Invoke(this, new object[] { dslExpression, tsc })!;
        }

        private Func<object, bool> CompileTypedPredicateRuntimeAdapter<TSelf>(string dslExpression, TargetSelectionContext tsc)
        {
            var typed = CompilePredicate<TSelf>(dslExpression, tsc);
            return item => item is TSelf casted && typed(casted);
        }

        private Func<object, IDslTraceCollector?, bool> CompileTypedPredicateWithTraceRuntimeAdapter<TSelf>(string dslExpression, TargetSelectionContext tsc)
        {
            var typed = CompilePredicateWithTrace<TSelf>(dslExpression, tsc);
            return (item, trace) => item is TSelf casted && typed(casted, trace);
        }

        private Func<object, object?> CompileTypedGetterRuntimeAdapter<TSelf>(string dslExpression, TargetSelectionContext tsc)
        {
            var typed = CompileGetter<TSelf>(dslExpression, tsc);
            return item =>
            {
                if (item is TSelf casted)
                {
                    return typed(casted);
                }

                return null;
            };
        }

        private Func<object, IDslTraceCollector?, object?> CompileTypedGetterWithTraceRuntimeAdapter<TSelf>(string dslExpression, TargetSelectionContext tsc)
        {
            var typed = CompileGetterWithTrace<TSelf>(dslExpression, tsc);
            return (item, trace) =>
            {
                if (item is TSelf casted)
                {
                    return typed(casted, trace);
                }

                return null;
            };
        }

        private void EnsureSelfUsageIsValid(
            string dslExpression,
            Type? selfType,
            IDslCompiler.SelfResolutionMode selfResolutionMode)
        {
            if (string.IsNullOrWhiteSpace(dslExpression))
            {
                return;
            }

            if (selfResolutionMode == IDslCompiler.SelfResolutionMode.ItemRequired && selfType == null)
            {
                throw new InvalidOperationException(
                    $"DSL expression '{dslExpression}' requires a non-null self type, but no runtime item type was provided.");
            }

            if (selfResolutionMode != IDslCompiler.SelfResolutionMode.ContextOnly)
            {
                return;
            }

            if (!ExpressionContainsForbiddenSelfReference(dslExpression))
            {
                return;
            }

            throw new InvalidOperationException(
                $"DSL expression '{dslExpression}' references 'self/this/current', but current execution mode does not provide self context.");
        }

        private bool ExpressionContainsForbiddenSelfReference(string dslExpression)
        {
            var cacheKey = $"DSL_FORBIDDEN_SELF_REF:{dslExpression}";
            if (_cache.TryGetValue<bool>(cacheKey, out var cached))
            {
                return cached;
            }

            bool hasForbiddenSelf;
            try
            {
                var lexer = new Lexer(dslExpression);
                var tokens = lexer.Tokenize();
                var parser = new Parser.Parser(tokens);
                var ast = parser.ParseSingleExpression();
                hasForbiddenSelf = AstContainsForbiddenSelfReference(ast, isSelfBoundScope: false);
            }
            catch
            {
                hasForbiddenSelf = false;
            }

            _cache.Set(cacheKey, hasForbiddenSelf);
            return hasForbiddenSelf;
        }

        private static bool AstContainsForbiddenSelfReference(AstNode? node, bool isSelfBoundScope)
        {
            if (node == null)
            {
                return false;
            }

            if (node is QueryFilterNode queryFilter)
            {
                return AstContainsForbiddenSelfReference(queryFilter.Source, isSelfBoundScope)
                    || AstContainsForbiddenSelfReference(queryFilter.Filter, isSelfBoundScope: true);
            }

            if (node is QueryOrderByNode queryOrderBy)
            {
                if (AstContainsForbiddenSelfReference(queryOrderBy.Source, isSelfBoundScope))
                {
                    return true;
                }

                foreach (var ordering in queryOrderBy.Orderings)
                {
                    if (AstContainsForbiddenSelfReference(ordering.Expression, isSelfBoundScope: true))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (node is QueryDistinctNode queryDistinct)
            {
                return AstContainsForbiddenSelfReference(queryDistinct.Source, isSelfBoundScope)
                    || AstContainsForbiddenSelfReference(queryDistinct.KeySelector, isSelfBoundScope: true);
            }

            if (node is QuerySelectNode querySelect)
            {
                return AstContainsForbiddenSelfReference(querySelect.Source, isSelfBoundScope)
                    || AstContainsForbiddenSelfReference(querySelect.Selector, isSelfBoundScope: true);
            }

            if (node is QueryTakeNode queryTake)
            {
                if (AstContainsForbiddenSelfReference(queryTake.Source, isSelfBoundScope))
                {
                    return true;
                }

                return AstContainsForbiddenSelfReference(queryTake.Selection, isSelfBoundScope);
            }

            if (node is IdentifierNode identifier)
            {
                var root = identifier.Path.Count > 0 ? identifier.Path[0] : identifier.Name;
                if (IsSelfRoot(root) && !isSelfBoundScope)
                {
                    return true;
                }
            }

            foreach (var child in node.Children)
            {
                if (AstContainsForbiddenSelfReference(child, isSelfBoundScope))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSelfRoot(string? root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return false;
            }

            return root.Equals("self", StringComparison.OrdinalIgnoreCase)
                || root.Equals("this", StringComparison.OrdinalIgnoreCase)
                || root.Equals("current", StringComparison.OrdinalIgnoreCase);
        }
    }
}
