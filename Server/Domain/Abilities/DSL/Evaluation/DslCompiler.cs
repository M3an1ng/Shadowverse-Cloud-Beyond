using System;
using System.Collections.Concurrent;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;
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
    }
}
