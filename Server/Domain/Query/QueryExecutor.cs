//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
//
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities; // For CardDefinition, CardInstance if needed
using ShadowverseBeyond.Server.Dtos; // For CardInstanceDto, CardDefinitionDto, GameStateDto
using ShadowverseBeyond.Server.Domain.Interfaces; // Correct namespace for ICardRepository
using ShadowverseBeyond.Server.Dtos.Enums; // Added for CardType enum
using ShadowverseBeyond.Server.Domain.Abilities.Common; // Added for ValueConfig
using CommonAbilities = ShadowverseBeyond.Server.Domain.Abilities.Common; // Alias for Common abilities namespace
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser; // Added for Lexer and Parser
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST; // Added for AstNode and IdentifierNode
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation; // Added for DslEvaluator and EvaluationContext
using ShadowverseBeyond.Server.Domain.Events; // Added for GameContext
using ShadowverseBeyond.Server.Domain.Query;
using AutoMapper;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Infrastructure.Logging;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;

namespace ShadowverseBeyond.Server.Domain.Query
{
    /// <summary>
    /// ICardQueryExecutor 的具体实现，负责处理卡牌查询逻辑。
    /// </summary>
    public class QueryExecutor : IQueryExecutor
    {
        private readonly ILogger<QueryExecutor> _logger;
        private readonly IDslCompiler _dslCompiler;
        private readonly IMapper _mapper;
        private readonly IQueryDslParser _dslParser;

        public QueryExecutor(
            ILogger<QueryExecutor> logger,
            IDslCompiler dslCompiler,
            IMapper mapper,
            IQueryDslParser dslParser)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dslCompiler = dslCompiler ?? throw new ArgumentNullException(nameof(dslCompiler));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _dslParser = dslParser ?? throw new ArgumentNullException(nameof(dslParser));
        }

        /// <inheritdoc />
        public QueryResult ExecuteQuery(string dsl, GameContext context, Dictionary<string, List<Abilities.Targets.TargetInfo>>? resolvedTargets = null)
        {
            var tsc = new TargetSelectionContext(context, resolvedTargets);
            var traceEntries = CreateTraceEntries(context);
            var unifiedResult = ExecuteUnifiedDsl(dsl, tsc, traceEntries);
            if (unifiedResult.Success)
            {
                return unifiedResult;
            }

            try
            {
                var queryConfig = _dslParser.Parse(dsl);
                return ExecuteQuery(queryConfig, context, resolvedTargets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse or execute DSL query: {Dsl}", dsl);
                return new QueryResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to parse or execute DSL query: {ex.Message}",
                    TraceEntries = traceEntries ?? new List<ConditionTraceEntryDto>()
                };
            }
        }

        /// <inheritdoc />
        public QueryResult ExecuteQuery(QueryConfig queryConfig, GameContext context, Dictionary<string, List<Abilities.Targets.TargetInfo>>? resolvedTargets = null)
        {
            queryConfig = NormalizeQueryConfig(queryConfig);
            _logger.LogDebug("Executing card query pipeline. Source DSL: {SourcePoolExpression}, Operations: {OperationCount}",
                queryConfig.SourcePoolExpression, queryConfig.Operations?.Count ?? 0);

            // Create the TargetSelectionContext to pass through the pipeline.
            var tsc = new TargetSelectionContext(context, resolvedTargets);
            var traceEntries = CreateTraceEntries(context);

            if (queryConfig.Operations == null || queryConfig.Operations.Count == 0)
            {
                return ExecuteUnifiedDsl(queryConfig.SourcePoolExpression, tsc, traceEntries);
            }

            List<object> currentPool;

            try
            {
                // --- 1. Execute Source DSL to get initial pool ---
                currentPool = ExecuteSourceDsl(queryConfig.SourcePoolExpression, tsc, traceEntries);
                _logger.LogTrace("Initial pool size after DSL execution: {PoolSize}", currentPool.Count);

                // --- 2. Apply Pipeline Operations ---
                if (queryConfig.Operations != null && queryConfig.Operations.Any())
                {
                    foreach (var operation in queryConfig.Operations)
                    {
                        _logger.LogTrace("Applying operation: {OperationType}", operation.GetType().Name);

                        var originalPoolCount = currentPool.Count;

                        if (operation is FilterOperation filterOp)
                        {
                            currentPool = ApplyFilterOperation(currentPool, filterOp, tsc, traceEntries);
                        }
                        else if (operation is SortOperation sortOp)
                        {
                            currentPool = ApplySortOperation(currentPool, sortOp, tsc, traceEntries);
                        }
                        else if (operation is DistinctOperation distinctOp)
                        {
                            currentPool = ApplyDistinctOperation(currentPool, distinctOp, tsc, traceEntries);
                        }
                        else if (operation is SelectOperation selectOp)
                        {
                            currentPool = ApplySelectOperation(currentPool, selectOp, tsc, traceEntries);
                        }
                        else if (operation is SelectionOperation selectionOp)
                        {
                            currentPool = ApplySelectionOperation(currentPool, selectionOp, tsc, traceEntries);
                        }
                        else if (operation is PostProcessingOperation postProcessingOp)
                        {
                            currentPool = ApplyPostProcessingOperation(currentPool, postProcessingOp, tsc, traceEntries);
                        }

                        // Log after applying the operation OR if it was skipped
                        if (operation is not FilterOperation && operation is not SortOperation && operation is not DistinctOperation && operation is not SelectOperation && operation is not SelectionOperation && operation is not PostProcessingOperation)
                        {
                            _logger.LogWarning("Unsupported query operation type encountered: {OperationType}. Pool remains unchanged.", operation.GetType().FullName);
                        }
                        else
                        {
                            _logger.LogTrace("Pool size after {OperationType}: {PoolSize} (was {OriginalCount})", operation.GetType().Name, currentPool.Count, originalPoolCount);
                        }
                    } // Closing brace for foreach loop
                } // Closing brace for if (queryConfig.Operations != null && queryConfig.Operations.Any())
                else
                {
                    _logger.LogTrace("No operations defined or operations list is empty in the query config.");
                } // Closing brace for else

                // --- 3. Build Result ---
                var resultsAsObjects = currentPool.Cast<object>().ToList();
                _logger.LogDebug("Card query pipeline executed successfully. Returning {ResultCount} results.", resultsAsObjects.Count);

                return new QueryResult // Success result returned from try block
                {
                    Results = resultsAsObjects.AsReadOnly(),
                    Success = true,
                    TraceEntries = traceEntries ?? new List<ConditionTraceEntryDto>()
                };
            } // Correctly closing the try block here
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing card query pipeline. Exception Details: {ExceptionDetails}", ex.ToString());
                // Return error result from catch block
                return new QueryResult
                {
                    Results = Array.Empty<object>(), // Empty results on error
                    Success = false,
                    ErrorMessage = $"An error occurred during query execution: {ex.Message}",
                    TraceEntries = traceEntries ?? new List<ConditionTraceEntryDto>()
                };
            } // Closing brace for catch block
        } // Closing brace for ExecuteQuery method

        /// <summary>
        /// Executes the source DSL to retrieve the initial pool of cards.
        /// This is the entry point for the new pipeline model's source definition.
        /// </summary>
        /// <param name="dsl">The Domain Specific Language string defining the source.</param>
        /// <param name="tsc">The target selection context, containing game state and resolved targets.</param>
        /// <returns>A list of objects representing the initial card pool.</returns>
        private List<object> ExecuteSourceDsl(string dsl, Abilities.Targets.TargetSelectionContext tsc, List<ConditionTraceEntryDto>? traceEntries)
        {
            _logger.LogDebug("Executing Source DSL with compiler: {Dsl}", dsl);

            if (string.IsNullOrWhiteSpace(dsl))
            {
                _logger.LogWarning("Source DSL string is null or empty. Returning empty pool.");
                return new List<object>();
            }

            try
            {
                TryAddTraceEntry(traceEntries, dsl, tsc, null);
                // Compile the DSL using the TSC-aware overload.
                // The 'item' parameter is null because the source DSL is context-dependent, not item-dependent.
                _logger.LogDebug("[CONTEXT_TRACKING] In ExecuteSourceDsl for DSL '{Dsl}'. SourceCard in received TSC: {SourceCardInstanceId}",
                    dsl,
                    tsc.GameContext.SourceCard?.InstanceId);
                var sourceGetter = _dslCompiler.CompileGetterRuntime(
                    dsl,
                    tsc,
                    selfType: null,
                    IDslCompiler.SelfResolutionMode.ContextOnly);
                var result = sourceGetter(null!); // Pass null for the item context

                _logger.LogDebug("Source DSL evaluation successful. Result type: {ResultType}", result?.GetType().Name ?? "null");

                if (!DslCollectionCoercion.TryCoerceToObjectEnumerable(result, out var enumerable))
                {
                    throw new DslCollectionCoercionException($"Source DSL '{dsl}' evaluated to a string, which is not a valid collection.");
                }

                var list = enumerable.Cast<object>().ToList();
                _logger.LogDebug("Source DSL normalized to list. Count: {Count}", list.Count);
                return list;
            }
            catch (DslCollectionCoercionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compile or execute source DSL: {Dsl}", dsl);
                return new List<object>();
            }
        }

        private QueryResult ExecuteUnifiedDsl(string dsl, Abilities.Targets.TargetSelectionContext tsc, List<ConditionTraceEntryDto>? traceEntries)
        {
            if (string.IsNullOrWhiteSpace(dsl))
            {
                return new QueryResult
                {
                    Results = Array.Empty<object>(),
                    Success = true,
                    TraceEntries = traceEntries ?? new List<ConditionTraceEntryDto>()
                };
            }

            try
            {
                TryAddTraceEntry(traceEntries, dsl, tsc, null);
                var getter = _dslCompiler.CompileGetterRuntime(
                    dsl,
                    tsc,
                    selfType: null,
                    IDslCompiler.SelfResolutionMode.ContextOnly);
                var rawResult = getter(null!);

                if (!DslCollectionCoercion.TryCoerceToObjectEnumerable(rawResult, out var enumerable))
                {
                    return new QueryResult
                    {
                        Success = false,
                        ErrorMessage = $"DSL query '{dsl}' evaluated to a string, which is not a valid collection."
                    };
                }

                var results = enumerable.Cast<object>().ToList();
                return new QueryResult
                {
                    Results = results.AsReadOnly(),
                    Success = true,
                    TraceEntries = traceEntries ?? new List<ConditionTraceEntryDto>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute unified DSL query: {Dsl}", dsl);
                return new QueryResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to execute unified DSL query: {ex.Message}",
                    TraceEntries = traceEntries ?? new List<ConditionTraceEntryDto>()
                };
            }
        }


        // --- Private Helper Methods ---

        private QueryConfig NormalizeQueryConfig(QueryConfig queryConfig)
        {
            if (queryConfig == null)
            {
                throw new ArgumentNullException(nameof(queryConfig));
            }

            if (queryConfig.Operations != null)
            {
                return queryConfig;
            }

            return queryConfig with
            {
                Operations = new List<IQueryOperation>()
            };
        }

        private List<object> ApplyFilterOperation(List<object> pool, FilterOperation filterOp, Abilities.Targets.TargetSelectionContext tsc, List<ConditionTraceEntryDto>? traceEntries)
        {
            _logger.LogTrace("Applying filter operation with {FilterCount} filters.", filterOp.Filters?.Count ?? 0);

            if (filterOp.Filters == null || !filterOp.Filters.Any())
            {
                _logger.LogTrace("No filters provided in FilterOperation. Returning original pool.");
                return pool;
            }

            var currentFilteredPool = pool;

            foreach (var filterConfig in filterOp.Filters)
            {
                string? dsl = null;
                if (filterConfig.ValueSource is FormulaValueConfig formulaConfig)
                {
                    dsl = formulaConfig.Formula;
                }

                if (string.IsNullOrWhiteSpace(dsl))
                {
                    _logger.LogWarning("Filter configuration is missing a valid DSL expression in ValueSource.Formula. Skipping filter.");
                    continue;
                }

                try
                {
                    TryAddTraceEntry(traceEntries, dsl, tsc, currentFilteredPool.FirstOrDefault());
                    // Infer item type from current pool to keep self strongly typed when possible.
                    var predicate = CompilePredicateWithInference(dsl, tsc, currentFilteredPool);

                    // Apply the compiled predicate to the pool
                    currentFilteredPool = currentFilteredPool.Where(item => predicate(item)).ToList();

                    _logger.LogTrace("Pool size after applying compiled filter ('{Dsl}'): {PoolSize}", dsl, currentFilteredPool.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to compile or execute filter DSL: {Dsl}. Skipping this filter.", dsl);
                    // Continue with the pool as it was before this failed filter
                }

                if (!currentFilteredPool.Any())
                {
                    _logger.LogTrace("Pool became empty after filter. Stopping further filter applications.");
                    break;
                }
            }

            return currentFilteredPool;
        }




        private List<object> ApplySortOperation(List<object> pool, SortOperation sortOp, Abilities.Targets.TargetSelectionContext tsc, List<ConditionTraceEntryDto>? traceEntries)
        {
            _logger.LogTrace("Applying sort operation with {OrderingCount} criteria.", sortOp.Ordering?.Count ?? 0);

            if (sortOp.Ordering == null || !sortOp.Ordering.Any())
            {
                _logger.LogTrace("No ordering criteria provided. Returning original pool.");
                return pool;
            }

            IOrderedEnumerable<object>? sortedPool = null;

            for (int i = 0; i < sortOp.Ordering.Count; i++)
            {
                var orderingConfig = sortOp.Ordering[i];
                string? dsl = null;
                if (orderingConfig.ValueSource is FormulaValueConfig formulaConfig)
                {
                    dsl = formulaConfig.Formula;
                }

                if (string.IsNullOrWhiteSpace(dsl))
                {
                    _logger.LogWarning("Sort configuration at index {Index} is missing a valid DSL expression. Skipping this sort criterion.", i);
                    continue;
                }

                try
                {
                    TryAddTraceEntry(traceEntries, dsl, tsc, pool.FirstOrDefault());
                    var keySelector = CompileGetterWithInference(dsl, tsc, pool);

                    if (i == 0) // First criterion
                    {
                        sortedPool = orderingConfig.Direction == OrderingDirection.Ascending
                            ? pool.OrderBy(item => keySelector(item))
                            : pool.OrderByDescending(item => keySelector(item));
                    }
                    else // Subsequent criteria
                    {
                        if (sortedPool == null) continue;
                        sortedPool = orderingConfig.Direction == OrderingDirection.Ascending
                            ? sortedPool.ThenBy(item => keySelector(item))
                            : sortedPool.ThenByDescending(item => keySelector(item));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to compile or execute sort DSL: {Dsl}. Skipping this sort criterion.", dsl);
                }
            }

            return sortedPool?.ToList() ?? pool;
        }

        private List<object> ApplyDistinctOperation(List<object> pool, DistinctOperation distinctOp, Abilities.Targets.TargetSelectionContext tsc, List<ConditionTraceEntryDto>? traceEntries)
        {
            _logger.LogTrace("Applying distinct operation.");

            if (distinctOp.KeySelector == null)
            {
                _logger.LogWarning("Distinct operation is missing a key selector. Returning original pool.");
                return pool;
            }

            Func<object, object?> keySelector;
            if (distinctOp.KeySelector is FormulaValueConfig formulaConfig)
            {
                var dsl = formulaConfig.Formula;
                if (string.IsNullOrWhiteSpace(dsl))
                {
                    _logger.LogWarning("Distinct key selector DSL is empty. Returning original pool.");
                    return pool;
                }

                TryAddTraceEntry(traceEntries, dsl, tsc, pool.FirstOrDefault());
                var getter = CompileGetterWithInference(dsl, tsc, pool);
                keySelector = item => getter(item);
            }
            else if (distinctOp.KeySelector is StaticValueConfig staticConfig)
            {
                var staticValue = staticConfig.Value;
                keySelector = _ => staticValue;
            }
            else
            {
                _logger.LogWarning("Distinct key selector uses an unsupported ValueConfig type: {Type}", distinctOp.KeySelector.GetType().Name);
                return pool;
            }

            var seen = new HashSet<object?>();
            var results = new List<object>();

            foreach (var item in pool)
            {
                var key = keySelector(item);
                if (!DslQueryOperations.IsAllowedDistinctKey(key))
                {
                    var keyType = key?.GetType().Name ?? "null";
                    throw new InvalidOperationException($"Distinct key type '{keyType}' is not supported.");
                }

                if (seen.Add(key))
                {
                    results.Add(item);
                }
            }

            return results;
        }

        private List<object> ApplySelectOperation(List<object> pool, SelectOperation selectOp, Abilities.Targets.TargetSelectionContext tsc, List<ConditionTraceEntryDto>? traceEntries)
        {
            _logger.LogTrace("Applying select operation.");

            if (selectOp.Selector is FormulaValueConfig formulaConfig)
            {
                TryAddTraceEntry(traceEntries, formulaConfig.Formula, tsc, pool.FirstOrDefault());
            }

            var selector = BuildProjectionSelector(selectOp.Selector, tsc, pool);
            if (selector == null)
            {
                _logger.LogWarning("Select operation is missing a valid selector. Returning original pool.");
                return pool;
            }

            var results = new List<object>();
            foreach (var item in pool)
            {
                results.Add(selector(item) ?? (object?)null!);
            }

            return results;
        }

        // Renamed to ApplySelectionOperationAsync and made async
        private List<object> ApplySelectionOperation(List<object> pool, SelectionOperation selectionOp, Abilities.Targets.TargetSelectionContext tsc, List<ConditionTraceEntryDto>? traceEntries)
        {
            _logger.LogTrace("Applying selection operation: {Strategy}. CountSource provided: {HasCountSource}",
                selectionOp.Strategy, selectionOp.CountSource != null);

            int count = 1; // Default count
            if (selectionOp.CountSource != null)
            {
                string? dsl = null;
                if (selectionOp.CountSource is FormulaValueConfig formulaConfig)
                {
                    dsl = formulaConfig.Formula;
                }
                else if (selectionOp.CountSource is StaticValueConfig staticConfig)
                {
                    count = TypeConverter.ConvertToInt(staticConfig.Value);
                }
                if (!string.IsNullOrWhiteSpace(dsl))
                {
                    try
                    {
                        TryAddTraceEntry(traceEntries, dsl, tsc, null);
                        // We pass 'null' for the item because count calculation is context-dependent, not item-dependent.
                        var countGetter = _dslCompiler.CompileGetterRuntime(
                            dsl,
                            tsc,
                            selfType: null,
                            IDslCompiler.SelfResolutionMode.ContextOnly);
                        var resolvedCountRaw = countGetter(null!);
                        if (resolvedCountRaw is int resolvedCount && resolvedCount >= 0)
                        {
                            count = resolvedCount;
                            _logger.LogTrace("Resolved dynamic count for selection: {ResolvedCount}", count);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to resolve CountSource or result was not a valid integer ({ResolvedValue}). Using default count: 1", resolvedCountRaw ?? "null");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to compile or execute count DSL: {Dsl}. Using default count: 1", dsl);
                    }
                }
            }
            else
            {
                _logger.LogTrace("No CountSource provided for SelectionOperation. Using default count: {Count}", count);
            }

            if (count < 0)
            {
                _logger.LogWarning("Invalid Count ({Count}) for selection strategy {Strategy}. Returning original pool.", count, selectionOp.Strategy);
                return pool;
            }

            // --- Apply Strategy ---
            switch (selectionOp.Strategy)
            {
                case SelectionStrategy.First:
                    _logger.LogTrace("Applying Take/First strategy with count: {Count}", count);
                    return pool.Take(count).ToList();

                case SelectionStrategy.Last:
                    _logger.LogTrace("Applying Last strategy with count: {Count}", count);
                    return pool.Skip(Math.Max(0, pool.Count - count)).ToList();

                case SelectionStrategy.Random:
                    _logger.LogTrace("Applying Random strategy with count: {Count}", count);
                    if (count >= pool.Count) return pool;
                    if (count == 0) return new List<object>();
                    var random = new Random(); // Consider seeding for determinism if needed
                    return pool.OrderBy(x => random.Next()).Take(count).ToList();

                default:
                    _logger.LogWarning("Unsupported SelectionStrategy: {Strategy}. Returning pool as is.", selectionOp.Strategy);
                    return pool;
            }
        }

        // ApplyLimitOperation method removed as its functionality is merged into ApplySelectionOperation with SelectionStrategy.Take

        /// <summary>
        /// Applies post-processing operations to the pool.
        /// </summary>
        private List<object> ApplyPostProcessingOperation(List<object> pool, PostProcessingOperation postProcessingOp, Abilities.Targets.TargetSelectionContext tsc, List<ConditionTraceEntryDto>? traceEntries)
        {
            _logger.LogTrace("Applying Post Processing Operation (projection).");

            if (postProcessingOp.Projection is FormulaValueConfig formulaConfig)
            {
                TryAddTraceEntry(traceEntries, formulaConfig.Formula, tsc, pool.FirstOrDefault());
            }

            var selector = BuildProjectionSelector(postProcessingOp.Projection, tsc, pool);
            if (selector == null)
            {
                _logger.LogWarning("Post-processing projection is missing or invalid. Returning original pool.");
                return pool;
            }

            var resultPool = new List<object>();
            foreach (var item in pool)
            {
                resultPool.Add(selector(item) ?? (object?)null!);
            }

            return resultPool;
        }

        private Func<object, object?>? BuildProjectionSelector(ValueConfig? selectorConfig, Abilities.Targets.TargetSelectionContext tsc, IReadOnlyList<object>? pool = null)
        {
            if (selectorConfig == null)
            {
                return null;
            }

            if (selectorConfig is FormulaValueConfig formulaConfig)
            {
                var dsl = formulaConfig.Formula;
                if (string.IsNullOrWhiteSpace(dsl))
                {
                    return null;
                }

                var getter = CompileGetterWithInference(dsl, tsc, pool);
                return item => getter(item);
            }

            if (selectorConfig is StaticValueConfig staticConfig)
            {
                var staticValue = staticConfig.Value;
                return _ => staticValue;
            }

            _logger.LogWarning("Projection selector uses an unsupported ValueConfig type: {Type}", selectorConfig.GetType().Name);
            return null;
        }

        private List<ConditionTraceEntryDto>? CreateTraceEntries(GameContext context)
        {
            var traceSettings = context.ServiceProvider.GetService<IConditionTraceSettings>();
            if (traceSettings?.IsTargetEnabledFor(context) == true)
            {
                return new List<ConditionTraceEntryDto>();
            }

            return null;
        }

        private void TryAddTraceEntry(List<ConditionTraceEntryDto>? entries, string expression, Abilities.Targets.TargetSelectionContext tsc, object? item)
        {
            if (entries == null || string.IsNullOrWhiteSpace(expression))
            {
                return;
            }

            var collector = new DslTraceCollector();
            try
            {
                var getter = CompileGetterWithTraceInference(expression, tsc, item);
                _ = getter(item!, collector);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to collect trace for DSL expression: {Expression}", expression);
            }

            var trace = DslTraceFormatter.Format(expression, collector);
            entries.Add(new ConditionTraceEntryDto
            {
                Expression = expression,
                Trace = trace
            });
        }

        private Func<object, bool> CompilePredicateWithInference(string dsl, TargetSelectionContext tsc, IReadOnlyList<object>? pool)
        {
            var inferredType = InferSingleItemType(pool);
            return _dslCompiler.CompilePredicateRuntime(
                dsl,
                tsc,
                inferredType,
                IDslCompiler.SelfResolutionMode.ItemOptional);
        }

        private Func<object, object?> CompileGetterWithInference(string dsl, TargetSelectionContext tsc, IReadOnlyList<object>? pool)
        {
            var inferredType = InferSingleItemType(pool);
            return _dslCompiler.CompileGetterRuntime(
                dsl,
                tsc,
                inferredType,
                IDslCompiler.SelfResolutionMode.ItemOptional);
        }

        private Func<object, IDslTraceCollector?, object?> CompileGetterWithTraceInference(string dsl, TargetSelectionContext tsc, object? item)
        {
            return _dslCompiler.CompileGetterWithTraceRuntime(
                dsl,
                tsc,
                item?.GetType(),
                item == null
                    ? IDslCompiler.SelfResolutionMode.ContextOnly
                    : IDslCompiler.SelfResolutionMode.ItemOptional);
        }

        private static Type? InferSingleItemType(IReadOnlyList<object>? pool)
        {
            if (pool == null || pool.Count == 0)
            {
                return null;
            }

            Type? inferredType = null;
            foreach (var item in pool)
            {
                if (item == null)
                {
                    continue;
                }

                var itemType = item.GetType();
                if (inferredType == null)
                {
                    inferredType = itemType;
                    continue;
                }

                if (inferredType != itemType)
                {
                    return null;
                }
            }

            return inferredType;
        }

        // --- Common Helper Methods ---




        // --- End of Helper Methods ---

    } // Close class CardQueryExecutor
} // Close namespace ShadowverseBeyond.Server.Domain.Query
