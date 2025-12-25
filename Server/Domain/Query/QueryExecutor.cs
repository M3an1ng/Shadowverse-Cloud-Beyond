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
                    ErrorMessage = $"Failed to parse or execute DSL query: {ex.Message}"
                };
            }
        }

        /// <inheritdoc />
        public QueryResult ExecuteQuery(QueryConfig queryConfig, GameContext context, Dictionary<string, List<Abilities.Targets.TargetInfo>>? resolvedTargets = null)
        {
            _logger.LogDebug("Executing card query pipeline. Source DSL: {SourcePoolExpression}, Operations: {OperationCount}",
                queryConfig.SourcePoolExpression, queryConfig.Operations?.Count ?? 0);

            // Create the TargetSelectionContext to pass through the pipeline.
            var tsc = new TargetSelectionContext(context, resolvedTargets);

            List<object> currentPool;

            try
            {
                // --- 1. Execute Source DSL to get initial pool ---
                currentPool = ExecuteSourceDsl(queryConfig.SourcePoolExpression, tsc);
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
                            currentPool = ApplyFilterOperation(currentPool, filterOp, tsc);
                        }
                        else if (operation is SortOperation sortOp)
                        {
                            currentPool = ApplySortOperation(currentPool, sortOp, tsc);
                        }
                        else if (operation is SelectionOperation selectionOp)
                        {
                            currentPool = ApplySelectionOperation(currentPool, selectionOp, tsc);
                        }
                        else if (operation is PostProcessingOperation postProcessingOp)
                        {
                            currentPool = ApplyPostProcessingOperation(currentPool, postProcessingOp, tsc);
                        }

                        // Log after applying the operation OR if it was skipped
                        if (operation is not FilterOperation && operation is not SortOperation && operation is not SelectionOperation && operation is not PostProcessingOperation)
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
                    Success = true
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
                    ErrorMessage = $"An error occurred during query execution: {ex.Message}"
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
        private List<object> ExecuteSourceDsl(string dsl, Abilities.Targets.TargetSelectionContext tsc)
        {
            _logger.LogDebug("Executing Source DSL with compiler: {Dsl}", dsl);

            if (string.IsNullOrWhiteSpace(dsl))
            {
                _logger.LogWarning("Source DSL string is null or empty. Returning empty pool.");
                return new List<object>();
            }

            try
            {
                // Compile the DSL using the TSC-aware overload.
                // The 'item' parameter is null because the source DSL is context-dependent, not item-dependent.
                _logger.LogDebug("[CONTEXT_TRACKING] In ExecuteSourceDsl for DSL '{Dsl}'. SourceCard in received TSC: {SourceCardInstanceId}",
                    dsl,
                    tsc.GameContext.SourceCard?.InstanceId);
                var sourceGetter = _dslCompiler.CompileGetter<object>(dsl, tsc);
                var result = sourceGetter(null!); // Pass null for the item context

                _logger.LogDebug("Source DSL evaluation successful. Result type: {ResultType}", result?.GetType().Name ?? "null");

                // Process the result
                if (result is List<object> objectList)
                {
                    _logger.LogDebug("DSL result is List<object>. Count: {Count}", objectList.Count);
                    return objectList;
                }
                if (result is IEnumerable<object> objectEnumerable)
                {
                    var list = objectEnumerable.ToList();
                    _logger.LogDebug("Converted IEnumerable<object> to List<object>. Count: {Count}", list.Count);
                    return list;
                }
                if (result is System.Collections.IEnumerable enumerable && !(result is string))
                {
                    var list = enumerable.Cast<object>().ToList();
                    _logger.LogDebug("Casted non-generic IEnumerable (Type: {ResultType}) to List<object>. Count: {Count}", result.GetType().Name, list.Count);
                    return list;
                }
                if (result != null)
                {
                    // 【关键修复】将任何单个对象包装成列表，以统一处理
                    _logger.LogDebug("Source DSL resulted in a single item (Type: {ResultType}). Wrapping it in a list.", result.GetType().Name);
                    return new List<object> { result };
                }

                _logger.LogDebug("Source DSL evaluation resulted in null. Returning empty list.");
                return new List<object>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compile or execute source DSL: {Dsl}", dsl);
                return new List<object>();
            }
        }


        // --- Private Helper Methods ---

        private List<object> ApplyFilterOperation(List<object> pool, FilterOperation filterOp, Abilities.Targets.TargetSelectionContext tsc)
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
                    // Compile the DSL string into a high-performance delegate using the TSC-aware overload.
                    var predicate = _dslCompiler.CompilePredicate<object>(dsl, tsc);

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




        private List<object> ApplySortOperation(List<object> pool, SortOperation sortOp, Abilities.Targets.TargetSelectionContext tsc)
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
                    var keySelector = _dslCompiler.CompileGetter<object>(dsl, tsc);

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

        // Renamed to ApplySelectionOperationAsync and made async
        private List<object> ApplySelectionOperation(List<object> pool, SelectionOperation selectionOp, Abilities.Targets.TargetSelectionContext tsc)
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
                        // We pass 'null' for the item because count calculation is context-dependent, not item-dependent.
                        var countGetter = _dslCompiler.CompileGetter<object>(dsl, tsc);
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
        private List<object> ApplyPostProcessingOperation(List<object> pool, PostProcessingOperation postProcessingOp, Abilities.Targets.TargetSelectionContext tsc)
        {
            _logger.LogTrace("Applying Post Processing Operation: {Type}", postProcessingOp.Type);

            var resultPool = new List<object>();

            switch (postProcessingOp.Type)
            {
                case PostProcessingType.ToCardDefinitionDto:
                    _logger.LogDebug("Processing PostProcessingType.ToCardDefinitionDto for {PoolCount} items.", pool.Count);
                    foreach (var item in pool)
                    {
                        int? cardId = null;
                        string? sourceDescription = null; // For logging

                        if (item is CardInstance instance)
                        {
                            cardId = instance.CurrentDefinitionId; // Correct property name
                            sourceDescription = $"CardInstance (InstanceId: {instance.InstanceId})";
                        }
                        else if (item is CardInstanceDto instanceDto)
                        {
                            cardId = instanceDto.CardId; // Correct property name
                            sourceDescription = $"CardInstanceDto (InstanceId: {instanceDto.InstanceId})";
                        }
                        else if (item is CardDefinition definition) // If pool already contains definitions
                        {
                            cardId = definition.Id;
                            sourceDescription = $"CardDefinition (Id: {definition.Id})";
                            // Already a definition, map directly
                            resultPool.Add(_mapper.Map<CardDefinitionDto>(definition));
                            continue; // Skip repository lookup
                        }
                        else if (item is CardDefinitionDto definitionDto) // If pool already contains definition DTOs
                        {
                            // Already the target type, add directly
                            resultPool.Add(definitionDto);
                            sourceDescription = $"CardDefinitionDto (CardId: {definitionDto.CardId})"; // Correct property name
                            cardId = definitionDto.CardId; // Correct property name
                            continue; // Skip repository lookup
                        }
                        else if (item is int id) // If pool contains raw IDs
                        {
                            cardId = id;
                            sourceDescription = $"int (Id: {id})";
                        }
                        // Add other potential source types if necessary

                        if (cardId.HasValue)
                        {
                            try
                            {
                                var definition = GameDataProvider.Instance.GetCardDefinition(cardId.Value);
                                if (definition != null)
                                {
                                    resultPool.Add(_mapper.Map<CardDefinitionDto>(definition));
                                    _logger.LogTrace("Successfully converted item {SourceDescription} to CardDefinitionDto (CardId: {CardId})", sourceDescription, cardId.Value); // Log CardId
                                }
                                else
                                {
                                    _logger.LogWarning("Card definition not found in data provider for ID: {CardId}, originating from {SourceDescription}.", cardId.Value, sourceDescription);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error retrieving card definition for ID: {CardId} from data provider, originating from {SourceDescription}.", cardId.Value, sourceDescription);
                                // Skip this item on error
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Could not determine CardId or item was already processed for item of type {ItemType} in pool during ToCardDefinitionDto conversion.", item?.GetType().Name ?? "null");
                        }
                    }
                    return resultPool; // Return the new list of CardDefinitionDto objects

                case PostProcessingType.ToCardInstanceDto:
                    _logger.LogDebug("Processing PostProcessingType.ToCardInstanceDto for {PoolCount} items.", pool.Count);
                    foreach (var item in pool)
                    {
                        if (item is CardInstanceDto instanceDto)
                        {
                            resultPool.Add(instanceDto); // Already the target type
                        }
                        else if (item is CardInstance instance)
                        {
                            resultPool.Add(_mapper.Map<CardInstanceDto>(instance));
                            _logger.LogTrace("Converted CardInstance (InstanceId: {InstanceId}) to CardInstanceDto using complete mapping.", instance.InstanceId);
                        }
                        else
                        {
                            _logger.LogWarning("Item of type {ItemType} cannot be converted to CardInstanceDto. Keeping original item.", item?.GetType().Name ?? "null");
                            if (item != null)
                            {
                                resultPool.Add(item); // Keep original if not convertible
                            }
                        }
                    }
                    return resultPool; // Return the new list

                case PostProcessingType.None:
                default:
                    _logger.LogTrace("PostProcessingType is None or unsupported ({Type}). No post-processing applied.", postProcessingOp.Type);
                    return pool; // Return original pool
            }
        }



        // --- Common Helper Methods ---




        // --- End of Helper Methods ---

    } // Close class CardQueryExecutor
} // Close namespace ShadowverseBeyond.Server.Domain.Query