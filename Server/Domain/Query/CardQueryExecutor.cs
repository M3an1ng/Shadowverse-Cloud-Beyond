//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
                                              // Removed incorrect using: using ShadowverseBeyond.Server.Domain.Query.Operations;
using ShadowverseBeyond.Server.Domain.Query; // Added for IQueryOperation and implementations
using AutoMapper; // Added for IMapper
using ShadowverseBeyond.Server.Extensions; // Added for CardInstance extension methods
using ShadowverseBeyond.Server.Services.Abstractions; // Added for IGameDataProvider

namespace ShadowverseBeyond.Server.Domain.Query
{
    /// <summary>
    /// ICardQueryExecutor 的具体实现，负责处理卡牌查询逻辑。
    /// </summary>
    public class CardQueryExecutor : ICardQueryExecutor
    {
        private readonly ILogger<CardQueryExecutor> _logger;
        private readonly DslEvaluator _dslEvaluator; // Inject DslEvaluator
        private readonly Abilities.DSL.Functions.IFunctionRegistry _functionRegistry; // Inject Function Registry
        private readonly IMapper _mapper; // Added IMapper field
        private readonly IGameDataProvider _gameDataProvider; // Added for complete CardInstance mapping

        public CardQueryExecutor(
            ILogger<CardQueryExecutor> logger,
            DslEvaluator dslEvaluator,
            Abilities.DSL.Functions.IFunctionRegistry functionRegistry, // Add IFunctionRegistry to constructor
            IMapper mapper, // Inject IMapper
            IGameDataProvider gameDataProvider) // Add IGameDataProvider to constructor
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dslEvaluator = dslEvaluator ?? throw new ArgumentNullException(nameof(dslEvaluator)); // Store injected evaluator
            _functionRegistry = functionRegistry ?? throw new ArgumentNullException(nameof(functionRegistry)); // Store injected registry
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper)); // Store IMapper
            _gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider)); // Store IGameDataProvider
        }

        /// <inheritdoc />
        public async Task<CardQueryResult> ExecuteQueryAsync(CardQueryConfig queryConfig, GameContext context)
        {
            _logger.LogDebug("Executing card query pipeline. TargetType: {TargetType}, Source DSL: {SourcePoolExpression}, Operations: {OperationCount}",
                queryConfig.TargetType, queryConfig.SourcePoolExpression, queryConfig.Operations?.Count ?? 0);

            List<object> currentPool = new List<object>(); // Initialize pool

            try
            {
                // --- 1. Execute Source DSL to get initial pool ---
                // Use the new ExecuteSourceDslAsync method
                currentPool = await ExecuteSourceDslAsync(queryConfig.SourcePoolExpression, context); // Changed method call
                _logger.LogTrace("Initial pool size after DSL execution: {PoolSize}", currentPool.Count);

                // --- 2. Apply Pipeline Operations ---
                if (queryConfig.Operations != null && queryConfig.Operations.Any())
                {
                    foreach (var operation in queryConfig.Operations)
                    {
                        _logger.LogTrace("Applying operation: {OperationType}", operation.GetType().Name);

                        // Apply the operation and update the pool using a switch expression
                        // Store count before operation
                        var originalPoolCount = currentPool.Count;

                        // Apply the operation and update the pool using a switch expression
                        if (operation is FilterOperation filterOp)
                        {
                            currentPool = await ApplyFilterOperationAsync(currentPool, filterOp, context);
                        }
                        else if (operation is SortOperation sortOp)
                        {
                            currentPool = await ApplySortOperationAsync(currentPool, sortOp, context);
                        }
                        else if (operation is SelectionOperation selectionOp)
                        {
                            currentPool = await ApplySelectionOperationAsync(currentPool, selectionOp, context);
                        }
                        else if (operation is PostProcessingOperation postProcessingOp)
                        {
                            currentPool = ApplyPostProcessingOperation(currentPool, postProcessingOp, context);
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

                return new CardQueryResult // Success result returned from try block
                {
                    ResultType = queryConfig.TargetType,
                    Results = resultsAsObjects.AsReadOnly(),
                    Success = true
                };
            } // Correctly closing the try block here
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing card query pipeline.");
                // Return error result from catch block
                return new CardQueryResult
                {
                    ResultType = queryConfig.TargetType, // Still report intended type
                    Results = Array.Empty<object>(), // Empty results on error
                    Success = false,
                    ErrorMessage = $"An error occurred during query execution: {ex.Message}"
                };
            } // Closing brace for catch block
        } // Closing brace for ExecuteQueryAsync method

        /// <summary>
        /// Executes the source DSL to retrieve the initial pool of cards.
        /// This is the entry point for the new pipeline model's source definition.
        /// </summary>
        /// <param name="dsl">The Domain Specific Language string defining the source.</param>
        /// <param name="context">The unified game context.</param>
        /// <returns>A list of objects representing the initial card pool.</returns>
        private async Task<List<object>> ExecuteSourceDslAsync(string dsl, GameContext context)
        {
            _logger.LogDebug("Executing Source DSL: {Dsl}", dsl);

            if (string.IsNullOrWhiteSpace(dsl))
            {
                _logger.LogWarning("Source DSL string is null or empty. Returning empty pool.");
                return new List<object>();
            }

            // 使用公共DSL解析方法
            var (success, result) = await ExecuteDslParsingAsync(dsl, context, "Source DSL");
            if (!success)
            {
                return new List<object>(); // 返回空列表
            }

            // 4. Process Result
            _logger.LogDebug("Source DSL evaluation successful. Result type: {ResultType}", result?.GetType().Name ?? "null");

            // Check if the result is already the desired type
            if (result is List<object> objectList)
            {
                _logger.LogDebug("DSL result is already List<object>. Count: {Count}", objectList.Count);
                return objectList;
            }
            // Check if it's IEnumerable<object> and convert
            else if (result is IEnumerable<object> objectEnumerable)
            {
                var list = objectEnumerable.ToList();
                _logger.LogDebug("Converted IEnumerable<object> DSL result to List<object>. Count: {Count}", list.Count);
                return list;
            }
            // Check if it's a non-string IEnumerable and attempt conversion
            else if (result is System.Collections.IEnumerable enumerable && !(result is string))
            {
                try
                {
                    var list = enumerable.Cast<object>().ToList();
                    _logger.LogDebug("Successfully casted non-generic IEnumerable DSL result (Type: {ResultType}) to List<object>. Count: {Count}", result?.GetType().Name, list.Count);
                    return list;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cast non-generic IEnumerable DSL evaluation result (Type: {ResultType}) to List<object>.", result?.GetType().Name);
                    // Decide if this should be an error or just return empty
                    return new List<object>();
                }
            }
            else if (result != null)
            {
                // Result is a single item, not a collection. This is unexpected for a source DSL.
                _logger.LogWarning("Source DSL evaluation resulted in a single item (Type: {ResultType}) instead of a collection. DSL: {Dsl}. Returning empty list.", result.GetType().Name, dsl);
                // Return empty list as it didn't resolve to a valid pool source.
                return new List<object>();
            }
            else // Result value is null
            {
                _logger.LogDebug("Source DSL evaluation resulted in null. Returning empty list.");
                return new List<object>();
            }
        }


        // --- Private Helper Methods ---

        private async Task<List<object>> ApplyFilterOperationAsync(List<object> pool, FilterOperation filterOp, GameContext context)
        {
            _logger.LogTrace("Applying filter operation with {FilterCount} filters.", filterOp.Filters?.Count ?? 0);

            if (filterOp.Filters == null || !filterOp.Filters.Any())
            {
                _logger.LogTrace("No filters provided in FilterOperation. Returning original pool.");
                return pool;
            }

            var currentFilteredPool = new List<object>(pool); // Start with a copy

            foreach (var filterConfig in filterOp.Filters)
            {
                var nextFilteredPool = new List<object>();
                foreach (var item in currentFilteredPool)
                {
                    // Resolve the value from the item being filtered
                    var itemValue = await ResolveValueSourceAsync(filterConfig.ValueSource, context, item);
                    // Resolve the target value to compare against
                    var targetValue = await ResolveValueSourceAsync(filterConfig.TargetValue, context, item); // Pass item for context if TargetValue is dynamic relative to item

                    _logger.LogTrace("Applying filter: ItemValue={ItemValue}, TargetValue={TargetValue}, Operator={Operator}", itemValue ?? "null", targetValue ?? "null", filterConfig.Operator);

                    if (CompareResolvedValues(itemValue, targetValue, filterConfig.Operator))
                    {
                        nextFilteredPool.Add(item);
                    }
                } // End inner foreach (item in currentFilteredPool)
                currentFilteredPool = nextFilteredPool; // Update the pool for the next filter (AND logic)

                _logger.LogTrace("Pool size after applying single filter ({Operator}): {PoolSize}", filterConfig.Operator, currentFilteredPool.Count);
                if (!currentFilteredPool.Any()) // Optimization: if pool is empty, no need to process further filters
                {
                    _logger.LogTrace("Pool became empty after filter. Stopping filter application.");
                    break; // Exit the outer foreach loop
                }
            } // End outer foreach (filterConfig in filterOp.Filters)

            return currentFilteredPool; // Return the final filtered pool
        } // End ApplyFilterOperationAsync method

        // --- Helper to resolve ValueConfig ---
        private async Task<object?> ResolveValueSourceAsync(ValueConfig? sourceConfig, GameContext context, object? currentItem = null) // Added async Task<>
        {
            if (sourceConfig == null) return null;

            _logger.LogTrace("Resolving ValueSource. Type: {ValueType}, Source: {DynamicSource}", sourceConfig.Type, sourceConfig.DynamicSource);

            object? resolvedValue = null;

            switch (sourceConfig.Type)
            {
                case CommonAbilities.ValueType.Static: // Use alias
                    resolvedValue = sourceConfig.StaticValue;
                    break;

                case CommonAbilities.ValueType.Dynamic: // Use alias
                    if (string.IsNullOrWhiteSpace(sourceConfig.DynamicSource))
                    {
                        _logger.LogWarning("DynamicSource path is empty.");
                        return null;
                    }
                    resolvedValue = await ResolveDynamicPathAsync(sourceConfig.DynamicSource, context, currentItem);
                    break;

                case CommonAbilities.ValueType.Formula: // Use alias
                    // 1. Parse the formula string into an AST
                    var formulaLexer = new Lexer(sourceConfig.Formula);
                    var formulaTokens = formulaLexer.Tokenize();
                    if (formulaLexer.Errors.Any())
                    {
                        _logger.LogError("Lexer errors encountered while tokenizing Formula: {Formula}. Errors: {Errors}",
                           sourceConfig.Formula, string.Join(", ", formulaLexer.Errors.Select(e => e.ToString())));
                        return null;
                    }
                    var formulaParser = new Parser(formulaTokens);
                    var formulaAst = formulaParser.ParseSingleExpression(); // Assuming single expression formula for now
                    if (formulaParser.Errors.Any() || formulaAst == null)
                    {
                        _logger.LogError("Failed to parse Formula: {Formula}. Errors: {Errors}",
                           sourceConfig.Formula, string.Join(", ", formulaParser.Errors));
                        return null;
                    }

                    // 2. Evaluate the AST using the injected DslEvaluator
                    _logger.LogDebug("Evaluating DSL Formula AST for: {Formula}", sourceConfig.Formula);

                    // 2b. Evaluate the AST
                    var evalResult = await _dslEvaluator.EvaluateAsync(formulaAst, context);

                    // 2c. Process the result
                    if (evalResult.IsSuccess)
                    {
                        resolvedValue = evalResult.Value;
                        _logger.LogDebug("DSL Formula '{Formula}' evaluated successfully. Result: {Result}", sourceConfig.Formula, resolvedValue ?? "null");
                    }
                    else
                    {
                        _logger.LogWarning("DSL Formula evaluation failed for '{Formula}'. Error: {Error}", sourceConfig.Formula, string.Join("; ", evalResult.Errors));
                        resolvedValue = null; // Ensure null on failure
                    }
                    break;

                case CommonAbilities.ValueType.Variable: // Use alias
                    resolvedValue = ResolveVariableValue(sourceConfig, context);
                    _logger.LogTrace("Resolved variable '{VariableName}' with value: {Value}",
                        sourceConfig.VariableName, resolvedValue ?? "null");
                    break;

                case CommonAbilities.ValueType.Random: // Use alias
                    if (sourceConfig.RandomConfig != null)
                    {
                        resolvedValue = GenerateRandomValue(sourceConfig.RandomConfig, context);
                        _logger.LogTrace("Generated random value {Value} using RandomConfig", resolvedValue);
                    }
                    else
                    {
                        _logger.LogWarning("RandomConfig is null for ValueType.Random.");
                    }
                    break;
                default:
                    _logger.LogWarning("Unsupported ValueType: {ValueType}", sourceConfig.Type);
                    break;
            }

            // Apply Min/Max constraints if the resolved value is numeric
            if (resolvedValue is int intValue)
            {
                resolvedValue = Math.Clamp(intValue, sourceConfig.MinValue, sourceConfig.MaxValue);
            }
            // Add similar clamps for other numeric types if needed (e.g., double, decimal)

            _logger.LogTrace("Resolved value: {ResolvedValue}", resolvedValue ?? "null");
            return resolvedValue;
        }

        /// <summary>
        /// 生成随机值，支持候选值列表、权重和种子
        /// </summary>
        /// <param name="randomConfig">随机配置</param>
        /// <param name="context">游戏上下文（用于确定性种子）</param>
        /// <returns>生成的随机值</returns>
        private object GenerateRandomValue(RandomConfig randomConfig, GameContext context)
        {
            // 【架构重构】使用当前玩家的回合数作为种子的一部分，确保游戏的确定性
            var currentPlayer = context.CurrentGameState.GetPlayerState(context.RequestingPlayerId);
            var turnNumber = currentPlayer?.TurnNumber ?? 0;
            var seed = randomConfig.Seed ?? (turnNumber * 31 + context.RequestingPlayerId.GetHashCode());
            var random = new Random(seed);

            // 如果有候选值列表，从中随机选择
            if (randomConfig.Candidates != null && randomConfig.Candidates.Count > 0)
            {
                if (randomConfig.Weights != null && randomConfig.Weights.Count == randomConfig.Candidates.Count)
                {
                    // 加权随机选择
                    return SelectWeightedRandom(randomConfig.Candidates, randomConfig.Weights, random);
                }
                else
                {
                    // 均匀随机选择
                    var index = random.Next(randomConfig.Candidates.Count);
                    return randomConfig.Candidates[index];
                }
            }

            // 否则在MinValue和MaxValue之间生成随机整数
            return random.Next(randomConfig.MinValue, randomConfig.MaxValue + 1); // +1 because Random.Next max is exclusive
        }

        /// <summary>
        /// 根据权重进行加权随机选择
        /// </summary>
        /// <param name="candidates">候选值列表</param>
        /// <param name="weights">权重列表</param>
        /// <param name="random">随机数生成器</param>
        /// <returns>选中的候选值</returns>
        private object SelectWeightedRandom(List<object> candidates, List<int> weights, Random random)
        {
            var totalWeight = weights.Sum();
            if (totalWeight <= 0)
            {
                // 如果权重总和为0或负数，回退到均匀选择
                _logger.LogWarning("Total weight is {TotalWeight}, falling back to uniform selection", totalWeight);
                return candidates[random.Next(candidates.Count)];
            }

            var randomValue = random.Next(totalWeight);
            var currentWeight = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                currentWeight += weights[i];
                if (randomValue < currentWeight)
                {
                    return candidates[i];
                }
            }

            // 理论上不应该到达这里，但作为安全措施
            return candidates[^1]; // 返回最后一个候选值
        }

        /// <summary>
        /// 解析变量值，支持临时变量和持久变量
        /// </summary>
        /// <param name="sourceConfig">值配置</param>
        /// <param name="context">游戏上下文</param>
        /// <returns>变量值，如果未找到则返回null</returns>
        private object? ResolveVariableValue(ValueConfig sourceConfig, GameContext context)
        {
            var variableName = sourceConfig.VariableName;

            if (string.IsNullOrEmpty(variableName))
            {
                _logger.LogWarning("Variable name is null or empty in ValueConfig");
                return null;
            }

            // 1. 首先检查临时变量（优先级更高）
            if (context.TemporaryVariables.TryGetValue(variableName, out var tempValue))
            {
                _logger.LogTrace("Found temporary variable '{VariableName}' with value: {Value}",
                    variableName, tempValue);
                return tempValue;
            }

            // 2. 检查源卡牌的持久变量（如果有源卡牌）
            if (context.SourceCard != null)
            {
                if (context.SourceCard.AbilityState.TryGetValue(variableName, out var persistentValue))
                {
                    _logger.LogTrace("Found persistent variable '{VariableName}' on source card with value: {Value}",
                        variableName, persistentValue);
                    return persistentValue;
                }
            }

            // 3. 检查当前处理项的变量（如果当前项是CardInstance）
            if (context.CurrentItem is CardInstance currentCard)
            {
                if (currentCard.AbilityState.TryGetValue(variableName, out var currentItemValue))
                {
                    _logger.LogTrace("Found variable '{VariableName}' on current item card with value: {Value}",
                        variableName, currentItemValue);
                    return currentItemValue;
                }
            }

            // 4. 检查已解析参数（可能包含变量值）
            if (context.ResolvedParameters.TryGetValue(variableName, out var paramValue))
            {
                _logger.LogTrace("Found variable '{VariableName}' in resolved parameters with value: {Value}",
                    variableName, paramValue);
                return paramValue;
            }

            _logger.LogDebug("Variable '{VariableName}' not found in any context", variableName);
            return null;
        }

        // --- Helper to resolve dynamic paths ---
        private async Task<object?> ResolveDynamicPathAsync(string path, GameContext context, object? currentItem)
        {
            var parts = path.Split('.');
            if (parts.Length == 0) return null;

            // 解析根对象
            var currentTarget = parts[0].ToLowerInvariant() == "self"
                ? currentItem
                : ResolveRootObject(parts[0], context, path);

            if (currentTarget == null)
            {
                return null;
            }

            // 遍历路径的其余部分
            for (int i = 1; i < parts.Length; i++)
            {
                currentTarget = GetPropertyValueSafely(currentTarget, parts[i], path);
                if (currentTarget == null)
                {
                    return null;
                }
            }

            return currentTarget; // 遍历路径后的最终值
        }
        // Helper for numeric comparisons
        private bool CompareValues(int cardValue, int targetValue, ComparisonOperator op)
        {
            return op switch
            {
                ComparisonOperator.EqualTo => cardValue == targetValue,
                ComparisonOperator.NotEqualTo => cardValue != targetValue,
                ComparisonOperator.GreaterThan => cardValue > targetValue,
                ComparisonOperator.GreaterThanOrEqualTo => cardValue >= targetValue,
                ComparisonOperator.LessThan => cardValue < targetValue,
                ComparisonOperator.LessThanOrEqualTo => cardValue <= targetValue,
                // Other operators like IsNull, Contains are not handled here as this is for numeric comparison
                _ => false, // Default for unhandled or invalid operators in this context
            };
        }

        private async Task<List<object>> ApplySortOperationAsync(List<object> pool, SortOperation sortOp, GameContext context) // Changed signature to async Task
        {
            _logger.LogTrace("Applying sort operation with {OrderingCount} criteria.", sortOp.Ordering?.Count ?? 0);

            if (sortOp.Ordering == null || !sortOp.Ordering.Any())
            {
                _logger.LogTrace("No ordering criteria provided. Returning original pool.");
                return pool;
            }

            // Pre-resolve keys to handle async resolution
            var itemsWithKeys = new List<(object originalItem, List<object?> keys)>();
            foreach (var item in pool)
            {
                var keys = new List<object?>();
                foreach (var orderingConfig in sortOp.Ordering)
                {
                    var key = await ResolveValueSourceAsync(orderingConfig.ValueSource, context, item);
                    keys.Add(key);
                    _logger.LogTrace("Resolved sort key for item {Item}: ValueSource={ValueSource}, Key={Key}", item, orderingConfig.ValueSource, key ?? "null");
                }
                itemsWithKeys.Add((item, keys));
            }

            // Apply sorting using LINQ on the pre-resolved keys
            IOrderedEnumerable<(object originalItem, List<object?> keys)>? sortedItems = null;

            for (int i = 0; i < sortOp.Ordering.Count; i++)
            {
                var orderingConfig = sortOp.Ordering[i];
                int keyIndex = i; // Capture loop variable for lambda

                if (i == 0) // First criterion uses OrderBy/OrderByDescending
                {
                    sortedItems = orderingConfig.Direction == OrderingDirection.Ascending
                        ? itemsWithKeys.OrderBy(tuple => tuple.keys[keyIndex], Comparer<object?>.Default)
                        : itemsWithKeys.OrderByDescending(tuple => tuple.keys[keyIndex], Comparer<object?>.Default);
                }
                else // Subsequent criteria use ThenBy/ThenByDescending
                {
                    // sortedItems cannot be null here if sortOp.Ordering.Count > 0
                    sortedItems = orderingConfig.Direction == OrderingDirection.Ascending
                        ? sortedItems!.ThenBy(tuple => tuple.keys[keyIndex], Comparer<object?>.Default)
                        : sortedItems!.ThenByDescending(tuple => tuple.keys[keyIndex], Comparer<object?>.Default);
                }
                _logger.LogTrace("Applied sorting criterion {Index}: Direction={Direction}, ValueSource={ValueSource}", i, orderingConfig.Direction, orderingConfig.ValueSource);
            }

            // Extract the original items in the sorted order
            return sortedItems?.Select(tuple => tuple.originalItem).ToList() ?? pool; // Return original pool if sorting somehow failed (e.g., no criteria)
        }

        // Renamed to ApplySelectionOperationAsync and made async
        private async Task<List<object>> ApplySelectionOperationAsync(List<object> pool, SelectionOperation selectionOp, GameContext context)
        {
            _logger.LogTrace("Applying selection operation: {Strategy}. CountSource provided: {HasCountSource}",
                selectionOp.Strategy, selectionOp.CountSource != null); // Removed direct Count access

            // --- Resolve Count ---
            int count = 1; // Default count if CountSource is null or resolves incorrectly
            if (selectionOp.CountSource != null)
            {
                // Added await here
                object? resolvedCountRaw = await ResolveValueSourceAsync(selectionOp.CountSource, context);
                if (resolvedCountRaw is int resolvedCount && resolvedCount >= 0)
                {
                    count = resolvedCount;
                    _logger.LogTrace("Resolved dynamic count for selection: {ResolvedCount}", count);
                }
                else
                {
                    _logger.LogWarning("Failed to resolve CountSource or result was invalid ({ResolvedValue}). Using default count: 1", resolvedCountRaw ?? "null");
                    // count remains 1 (default)
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
                case SelectionStrategy.First: // Treat First as Take
                    _logger.LogTrace("Applying Take/First strategy with count: {Count}", count);
                    return pool.Take(count).ToList();

                case SelectionStrategy.Last:
                    _logger.LogTrace("Applying Last strategy with count: {Count}", count);
                    return pool.Skip(Math.Max(0, pool.Count - count)).ToList(); // Skip to get the last N items

                case SelectionStrategy.Random:
                    _logger.LogTrace("Applying Random strategy with count: {Count}", count);
                    if (count >= pool.Count)
                    {
                        return pool; // If count is >= pool size, return all
                    }
                    if (count == 0)
                    {
                        return new List<object>(); // Return empty if count is 0
                    }
                    // Simple random selection
                    var random = new Random(); // Consider seeding for determinism if needed
                    return pool.OrderBy(x => random.Next()).Take(count).ToList();

                default:
                    _logger.LogWarning("Unsupported SelectionStrategy: {Strategy}. Returning pool as is.", selectionOp.Strategy);
                    return pool;
            }
        }

        // ApplyLimitOperation method removed as its functionality is merged into ApplySelectionOperation with SelectionStrategy.Take

        /// <summary>
        /// Resolves a path expression (e.g., ["player", "deck"]) against the query context
        /// to retrieve the corresponding collection of items (cards, definitions, etc.).
        /// </summary>
        /// <param name="path">The list of strings representing the path segments.</param>
        /// <param name="context">The query context containing game state.</param>
        /// <returns>An enumerable collection of objects found at the specified path, or null if not found or invalid.</returns>
        private async Task<IEnumerable<object>?> ResolveSourcePathAsync(List<string> path, GameContext context)
        {
            _logger.LogTrace("Resolving source path: {Path}", string.Join(".", path));

            if (path == null || !path.Any())
            {
                _logger.LogWarning("Source path is null or empty.");
                return null;
            }

            object? currentTarget = null;
            string rootSegment = path[0].ToLowerInvariant();

            // --- 使用公共方法解析根对象 ---
            currentTarget = ResolveRootObject(rootSegment, context, string.Join(".", path));

            // 特殊处理allcards的单独路径情况
            if (rootSegment.ToLowerInvariant() == "allcards" && path.Count == 1)
            {
                return currentTarget as IEnumerable<object>;
            }

            if (currentTarget == null)
            {
                // Warning already logged in the switch statement
                return null;
            }

            // --- 使用公共方法遍历剩余路径段 ---
            for (int i = 1; i < path.Count; i++)
            {
                currentTarget = GetPropertyValueSafely(currentTarget, path[i], string.Join(".", path));
                if (currentTarget == null)
                {
                    return null;
                }
            }

            // --- Final Result Check ---
            if (currentTarget is IEnumerable<object> finalPool)
            {
                _logger.LogDebug("Successfully resolved path {Path} to an enumerable collection.", string.Join(".", path));
                return finalPool;
            }
            else if (currentTarget != null)
            {
                // If the final target is not null but not enumerable, it's an invalid path for a source pool
                _logger.LogWarning("Resolved path {Path} did not result in an enumerable collection. Final Type: {Type}", string.Join(".", path), currentTarget.GetType().Name);
                return null; // Return null as it's not a valid pool source
            }
            else // currentTarget is null after traversal
            {
                _logger.LogWarning("Resolved path {Path} resulted in a null value.", string.Join(".", path));
                return null; // Return null as the path resolved to null
            }
            // Removed misplaced catch block from here
        } // Close ResolveSourcePathAsync method - Ensure this is the correct closing brace

        /// <summary>
        /// Applies post-processing operations to the pool.
        /// </summary>
        private List<object> ApplyPostProcessingOperation(List<object> pool, PostProcessingOperation postProcessingOp, GameContext context)
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
                            cardId = instance.DefinitionId; // Correct property name
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
                            resultPool.Add(MapToCardDefinitionDto(definition));
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
                                var definition = _gameDataProvider.GetCardDefinition(cardId.Value);
                                if (definition != null)
                                {
                                    resultPool.Add(MapToCardDefinitionDto(definition));
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
                            // 【修复】使用扩展方法进行完整映射，包含定义信息
                            resultPool.Add(instance.ToCompleteDto(_gameDataProvider, _mapper));
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

        /// <summary>
        /// Compares two resolved values using the specified operator.
        /// Handles nulls and basic type comparisons (numeric, string, bool).
        /// </summary>
        private bool CompareResolvedValues(object? value1, object? value2, ComparisonOperator op)
        {
            // Handle null comparisons first
            if (op == ComparisonOperator.EqualTo)
            {
                return Equals(value1, value2);
            }
            if (op == ComparisonOperator.NotEqualTo)
            {
                return !Equals(value1, value2);
            }

            // If either value is null for other operators (>, <, >=, <=), comparison fails
            if (value1 == null || value2 == null)
            {
                // Note: Specific handling for potential future IsNull/IsNotNull operators would go here.
                _logger.LogTrace("Comparison involving null for operator {Operator} results in false.", op);
                return false;
            }

            // Attempt numeric/comparable comparison
            if (TryConvertToComparable(value1, out IComparable? comp1) && TryConvertToComparable(value2, out IComparable? comp2))
            {
                try
                {
                    if (comp1 == null || comp2 == null) return false;
                    int comparisonResult = comp1.CompareTo(comp2); // Use CompareTo for >, <, >=, <=
                    return op switch
                    {
                        ComparisonOperator.GreaterThan => comparisonResult > 0,
                        ComparisonOperator.LessThan => comparisonResult < 0,
                        ComparisonOperator.GreaterThanOrEqualTo => comparisonResult >= 0, // Added
                        ComparisonOperator.LessThanOrEqualTo => comparisonResult <= 0,   // Added
                        // Equals/NotEquals handled above
                        _ => throw new ArgumentOutOfRangeException(nameof(op), $"Operator {op} not supported for IComparable comparison."),
                    };
                }
                catch (ArgumentException ex) // Catches cases where types are not comparable (e.g., comparing int and string)
                {
                    _logger.LogWarning(ex, "Failed to compare values of types {Type1} and {Type2} using IComparable.", value1.GetType(), value2.GetType());
                    return false; // Types not comparable
                }
            }

            // Note: String comparisons (Contains, StartsWith etc.) and collection comparisons (In, NotIn)
            // would require adding corresponding operators to the ComparisonOperator enum and implementing logic here.

            _logger.LogWarning("Comparison for operator {Operator} between types {Type1} and {Type2} is not implemented or failed.", op, value1.GetType(), value2.GetType());
            return false; // Default to false if comparison cannot be made
        }

        /// <summary>
        /// Helper to attempt conversion to IComparable for comparison logic.
        /// Focuses on common numeric types and bool.
        /// </summary>
        private bool TryConvertToComparable(object? value, out IComparable? comparable)
        {
            comparable = null;
            if (value == null) return false;

            if (value is int || value is long || value is short || value is byte ||
                value is float || value is double || value is decimal ||
                value is bool || value is DateTime || value is string) // Add other relevant IComparable types
            {
                comparable = value as IComparable;
                return comparable != null;
            }
            // Attempt conversion for string representations of numbers if needed, but be cautious
            // if (value is string str && double.TryParse(str, out double num)) { comparable = num; return true; }

            return false;
        }

        // --- Mapping Helpers ---
        /// <summary>
        /// 将CardDefinition实体映射为CardDefinitionDto
        /// </summary>
        /// <param name="definition">卡牌定义实体</param>
        /// <returns>卡牌定义DTO</returns>
        private CardDefinitionDto MapToCardDefinitionDto(CardDefinition definition)
        {
            // Basic manual mapping - expand as needed
            return new CardDefinitionDto
            {
                CardId = definition.Id, // Correct property name
                Name = definition.Name,
                Type = definition.Type, // Correct property name
                Cost = definition.Cost,
                Attack = definition.Attack, // Correct property name
                Health = definition.Health, // Correct property name
                Description = definition.Description,
                CanEvolve = definition.EvolveId.HasValue, // Derive CanEvolve from EvolveId
                Keywords = definition.Keywords ?? new List<string>(), // Handle null Keywords
                Rarity = definition.Rarity, // Use actual property from entity
                CardSet = definition.CardSet // Use actual property from entity
            };
        }


        // --- Common Helper Methods ---

        /// <summary>
        /// 执行DSL解析的通用方法，包含错误处理
        /// </summary>
        /// <param name="dsl">DSL表达式</param>
        /// <param name="context">游戏上下文</param>
        /// <param name="operationName">操作名称（用于日志）</param>
        /// <returns>解析和评估结果</returns>
        private async Task<(bool Success, object? Result)> ExecuteDslParsingAsync(string dsl, GameContext context, string operationName)
        {
            try
            {
                // 1. 词法分析
                var lexer = new Lexer(dsl);
                var tokens = lexer.Tokenize();
                if (lexer.Errors.Any())
                {
                    _logger.LogError("Lexer errors in {OperationName} DSL: {Dsl}. Errors: {Errors}",
                        operationName, dsl, string.Join("; ", lexer.Errors.Select(e => e.ToString())));
                    return (false, null);
                }

                // 2. 语法分析
                var parser = new Parser(tokens);
                var astNode = parser.ParseSingleExpression();
                if (parser.Errors.Any() || astNode == null)
                {
                    _logger.LogError("Parser errors in {OperationName} DSL: {Dsl}. Errors: {Errors}",
                        operationName, dsl, string.Join("; ", parser.Errors.Select(e => e.ToString())));
                    return (false, null);
                }

                // 3. 评估AST
                var evalResult = await _dslEvaluator.EvaluateAsync(astNode, context);
                if (!evalResult.IsSuccess)
                {
                    _logger.LogError("{OperationName} DSL evaluation failed: {Dsl}. Errors: {Errors}",
                        operationName, dsl, string.Join("; ", evalResult.Errors));
                    return (false, null);
                }

                return (true, evalResult.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during {OperationName} DSL parsing: {Dsl}", operationName, dsl);
                return (false, null);
            }
        }

        /// <summary>
        /// 安全地获取对象属性值，包含错误处理
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="propertyName">属性名称</param>
        /// <param name="contextPath">上下文路径（用于日志）</param>
        /// <returns>属性值，失败时返回null</returns>
        private object? GetPropertyValueSafely(object target, string propertyName, string contextPath)
        {
            if (target == null)
            {
                _logger.LogWarning("Target object is null when accessing property '{PropertyName}' in path: {Path}",
                    propertyName, contextPath);
                return null;
            }

            var propertyInfo = target.GetType().GetProperty(propertyName,
                System.Reflection.BindingFlags.IgnoreCase |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);

            if (propertyInfo == null)
            {
                // 处理特殊情况如List.Count
                if (propertyName.Equals("Count", StringComparison.OrdinalIgnoreCase) &&
                    target is System.Collections.ICollection collection)
                {
                    return collection.Count;
                }

                _logger.LogWarning("Property '{PropertyName}' not found on type '{TypeName}' for path: {Path}",
                    propertyName, target.GetType().Name, contextPath);
                return null;
            }

            try
            {
                return propertyInfo.GetValue(target);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting value for property '{PropertyName}' on type '{TypeName}' for path: {Path}",
                    propertyName, target.GetType().Name, contextPath);
                return null;
            }
        }

        /// <summary>
        /// 解析根对象的通用方法
        /// </summary>
        /// <param name="rootSegment">根段名称</param>
        /// <param name="context">游戏上下文</param>
        /// <param name="path">完整路径（用于日志）</param>
        /// <returns>解析的根对象</returns>
        private object? ResolveRootObject(string rootSegment, GameContext context, string path)
        {
            switch (rootSegment.ToLowerInvariant())
            {
                case "player":
                    var playerState = context.CurrentGameState.GetPlayerState(context.RequestingPlayerId);
                    var playerDto = playerState != null ? _mapper.Map<PlayerStateDto>(playerState) : null;
                    if (playerDto == null)
                        _logger.LogWarning("Requesting player state not found for path starting with 'player': {Path}", path);
                    return playerDto;

                case "opponent":
                    var opponentState = context.CurrentGameState.GetOpponentPlayerState(context.RequestingPlayerId);
                    var opponentDto = opponentState != null ? _mapper.Map<PlayerStateDto>(opponentState) : null;
                    if (opponentDto == null)
                        _logger.LogWarning("Opponent player state not found for path starting with 'opponent': {Path}", path);
                    return opponentDto;

                case "gamestate":
                    var gameStateDto = context.CreateSnapshot();
                    if (gameStateDto == null)
                        _logger.LogWarning("CurrentGameState is null in GameContext for path: {Path}", path);
                    return gameStateDto;

                case "allcards":
                    try
                    {
                        var definitions = _gameDataProvider.GetAllCardDefinitions().Values;
                        _logger.LogDebug("Resolved path 'allcards' to all card definitions ({Count}) for path: {Path}",
                            definitions.Count(), path);
                        return definitions.Cast<object>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error retrieving all card definitions from data provider for path: {Path}", path);
                        return null;
                    }

                case "triggeringevent":
                    _logger.LogWarning("Resolving 'triggeringEvent' path is not fully implemented: {Path}", path);
                    return context.TriggeringEvent;

                case "triggeringcard":
                    _logger.LogWarning("Resolving 'triggeringCard' path is not fully implemented: {Path}", path);
                    return null;

                default:
                    _logger.LogWarning("Unknown root segment in path: {RootSegment} for path: {Path}", rootSegment, path);
                    return null;
            }
        }

        // --- End of Helper Methods ---

    } // Close class CardQueryExecutor
} // Close namespace ShadowverseBeyond.Server.Domain.Query