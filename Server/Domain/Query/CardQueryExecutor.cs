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
                                                                // Removed incorrect using: using ShadowverseBeyond.Server.Domain.Query.Operations;
using ShadowverseBeyond.Server.Domain.Query; // Added for IQueryOperation and implementations

namespace ShadowverseBeyond.Server.Domain.Query
{
    /// <summary>
    /// ICardQueryExecutor 的具体实现，负责处理卡牌查询逻辑。
    /// </summary>
    public class CardQueryExecutor : ICardQueryExecutor
    {
        private readonly ILogger<CardQueryExecutor> _logger;
        // 假设有一个仓库用于获取卡牌定义数据
        private readonly ICardRepository _cardRepository;
        private readonly DslEvaluator _dslEvaluator; // Inject DslEvaluator

        public CardQueryExecutor(ILogger<CardQueryExecutor> logger, ICardRepository cardRepository, DslEvaluator dslEvaluator) // Add DslEvaluator to constructor
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cardRepository = cardRepository ?? throw new ArgumentNullException(nameof(cardRepository));
            _dslEvaluator = dslEvaluator ?? throw new ArgumentNullException(nameof(dslEvaluator)); // Store injected evaluator
        }

        /// <inheritdoc />
        public async Task<CardQueryResult> ExecuteQueryAsync(CardQueryConfig queryConfig, QueryContext context)
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
                        currentPool = operation switch
                        {
                            // Call the refactored/created methods
                            FilterOperation filterOp => await ApplyFilterOperationAsync(currentPool, filterOp, context),
                            SortOperation sortOp => await ApplySortOperation(currentPool, sortOp, context), // Now async
                            SelectionOperation selectionOp => await ApplySelectionOperationAsync(currentPool, selectionOp, context), // Renamed from ApplyLimit
                            PostProcessingOperation postProcessingOp => await ApplyPostProcessingOperationAsync(currentPool, postProcessingOp, context), // New method call
                            // For unsupported types, simply return the pool unchanged.
                            _ => currentPool
                        }; // End of switch expression

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
        /// <param name="context">The query context.</param>
        /// <returns>A list of objects representing the initial card pool.</returns>
        private async Task<List<object>> ExecuteSourceDslAsync(string dsl, QueryContext context)
        {
            _logger.LogDebug("Executing Source DSL: {Dsl}", dsl);

            if (string.IsNullOrWhiteSpace(dsl))
            {
                _logger.LogWarning("Source DSL string is null or empty. Returning empty pool.");
                return new List<object>();
            }

            // 1. Parse DSL
            var lexer = new Lexer(dsl);
            var tokens = lexer.Tokenize();
            if (lexer.Errors.Any())
            {
                _logger.LogError("Lexer errors encountered while tokenizing Source DSL: {Dsl}. Errors: {Errors}",
                   dsl, string.Join("; ", lexer.Errors.Select(e => e.ToString())));
                // Consider throwing an exception or returning a specific error result
                return new List<object>(); // Return empty on lexer error
            }

            var parser = new Parser(tokens);
            var astNode = parser.ParseSingleExpression(); // Use ParseSingleExpression for source DSL

            if (parser.Errors.Any() || astNode == null)
            {
                _logger.LogError("Parser errors encountered while parsing Source DSL: {Dsl}. Errors: {Errors}",
                   dsl, string.Join("; ", parser.Errors.Select(e => e.ToString())));
                // Consider throwing an exception or returning a specific error result
                return new List<object>(); // Return empty on parser error or null AST
            }

            // 2. Create EvaluationContext
            // Resolve player states from the GameStateDto in the QueryContext
            PlayerStateDto? requestingPlayer = context.CurrentGameState?.Players?.FirstOrDefault(p => p.PlayerId == context.RequestingPlayerId);
            PlayerStateDto? opponentPlayer = context.CurrentGameState?.Players?.FirstOrDefault(p => p.PlayerId != context.RequestingPlayerId);

            // Note: currentItem and abilityContext are typically null when evaluating the source pool DSL
            var evalContext = new EvaluationContext(
                _cardRepository,
                context.CurrentGameState,
                requestingPlayer,
                opponentPlayer,
                null, // No specific 'self' item when resolving the initial pool
                null  // No ability context when resolving the initial pool
            );

            // 3. Evaluate AST
            _logger.LogTrace("Evaluating parsed Source DSL AST.");
            EvaluationResult evalResult = await _dslEvaluator.EvaluateAsync(astNode, evalContext);

            // 4. Process Result
            if (evalResult.IsSuccess)
            {
                _logger.LogDebug("Source DSL evaluation successful. Result type: {ResultType}", evalResult.Value?.GetType().Name ?? "null");

                // Check if the result is already the desired type
                if (evalResult.Value is List<object> objectList)
                {
                    _logger.LogDebug("DSL result is already List<object>. Count: {Count}", objectList.Count);
                    return objectList;
                }
                // Check if it's IEnumerable<object> and convert
                else if (evalResult.Value is IEnumerable<object> objectEnumerable)
                {
                    var list = objectEnumerable.ToList();
                    _logger.LogDebug("Converted IEnumerable<object> DSL result to List<object>. Count: {Count}", list.Count);
                    return list;
                }
                // Check if it's a non-string IEnumerable and attempt conversion
                else if (evalResult.Value is System.Collections.IEnumerable enumerable && !(evalResult.Value is string))
                {
                    try
                    {
                        var list = enumerable.Cast<object>().ToList();
                        _logger.LogDebug("Successfully casted non-generic IEnumerable DSL result (Type: {ResultType}) to List<object>. Count: {Count}", evalResult.Value?.GetType().Name, list.Count);
                        return list;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cast non-generic IEnumerable DSL evaluation result (Type: {ResultType}) to List<object>.", evalResult.Value?.GetType().Name);
                        // Decide if this should be an error or just return empty
                        return new List<object>();
                    }
                }
                else if (evalResult.Value != null)
                {
                    // Result is a single item, not a collection. This is unexpected for a source DSL.
                    _logger.LogWarning("Source DSL evaluation resulted in a single item (Type: {ResultType}) instead of a collection. DSL: {Dsl}. Returning empty list.", evalResult.Value.GetType().Name, dsl);
                    // Return empty list as it didn't resolve to a valid pool source.
                    return new List<object>();
                }
                else // Result value is null
                {
                    _logger.LogDebug("Source DSL evaluation resulted in null. Returning empty list.");
                    return new List<object>();
                }
            }
            else
            {
                _logger.LogError("Source DSL evaluation failed. DSL: {Dsl}. Error: {Error}", dsl, string.Join("; ", evalResult.Errors));
                // Consider throwing an exception or returning a specific error result
                return new List<object>(); // Return empty list on evaluation failure
            }
        }


        // --- Private Helper Methods ---

        private async Task<List<object>> ApplyFilterOperationAsync(List<object> pool, FilterOperation filterOp, QueryContext context)
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
                    var itemValue = await ResolveValueSource(filterConfig.ValueSource, context, item);
                    // Resolve the target value to compare against
                    var targetValue = await ResolveValueSource(filterConfig.TargetValue, context, item); // Pass item for context if TargetValue is dynamic relative to item

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
        private async Task<object?> ResolveValueSource(ValueConfig? sourceConfig, QueryContext context, object? currentItem = null) // Added async Task<>
                                                                                                                                    // TODO: Implement the actual filtering logic based on FilterOperation.Filters
                                                                                                                                    // This method should iterate through filterOp.Filters and apply them.
                                                                                                                                    // The old ApplySingleFilter logic (commented out above) can be adapted.
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
                    resolvedValue = ResolveDynamicPath(sourceConfig.DynamicSource, context, currentItem);
                    break;

                case CommonAbilities.ValueType.Formula: // Use alias
                    _logger.LogWarning("ValueType.Formula evaluation requires DSL Evaluator implementation.");
                    // TODO: Implement formula evaluation
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

                    // 2a. Create EvaluationContext from QueryContext
                    var requestingPlayer = context.CurrentGameState?.Players?.FirstOrDefault(p => p.PlayerId == context.RequestingPlayerId);
                    var opponentPlayer = context.CurrentGameState?.Players?.FirstOrDefault(p => p.PlayerId != context.RequestingPlayerId);

                    // Note: QueryContext doesn't have AbilityContext, so pass null for now.
                    // Formulas evaluated in queries won't have access to ability-specific variables.
                    var evalContext = new EvaluationContext(
                        _cardRepository,
                        context.CurrentGameState,
                        requestingPlayer,
                        opponentPlayer,
                        currentItem, // Pass the current item being evaluated (e.g., the card in a filter)
                        null // No AbilityContext available in QueryContext
                    );

                    // 2b. Evaluate the AST
                    var evalResult = await _dslEvaluator.EvaluateAsync(formulaAst, evalContext);

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
                    _logger.LogWarning("ValueType.Variable retrieval not yet implemented in ResolveValueSource.");
                    // TODO: Implement variable retrieval from context or ability system
                    // Example placeholder:
                    // if (context.AbilityContext?.Variables.TryGetValue(sourceConfig.VariableName, out var variableValue) ?? false)
                    // {
                    //     resolvedValue = variableValue;
                    //     _logger.LogTrace("Retrieved variable '{VariableName}' with value: {Value}", sourceConfig.VariableName, resolvedValue);
                    // }
                    // else
                    // {
                    //     _logger.LogWarning("Variable '{VariableName}' not found in context.", sourceConfig.VariableName);
                    // }
                    resolvedValue = null; // Placeholder
                    break;

                case CommonAbilities.ValueType.Random: // Use alias
                                                       //_logger.LogWarning("ValueType.Random not yet implemented in ResolveValueSource."); // Removed warning as it's now implemented
                                                       // TODO: Implement random value generation based on RandomConfig
                    if (sourceConfig.RandomConfig != null)
                    {
                        // TODO: Consider using a shared Random instance or one seeded from game state for determinism if needed.
                        var random = sourceConfig.RandomConfig.Seed.HasValue
                            ? new Random(sourceConfig.RandomConfig.Seed.Value)
                            : new Random();
                        // TODO: Add support for Candidates and Weights if needed
                        resolvedValue = random.Next(sourceConfig.RandomConfig.MinValue, sourceConfig.RandomConfig.MaxValue + 1); // .Next max is exclusive
                        _logger.LogTrace("Generated random value {Value} between {Min} and {Max}", resolvedValue, sourceConfig.RandomConfig.MinValue, sourceConfig.RandomConfig.MaxValue);
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

        // --- Helper to resolve dynamic paths ---
        private object? ResolveDynamicPath(string path, QueryContext context, object? currentItem)
        {
            var parts = path.Split('.');
            if (parts.Length == 0) return null;

            object? currentTarget = null;
            string propertyName = string.Empty;

            // Determine the root object based on the first part of the path
            switch (parts[0].ToLowerInvariant())
            {
                case "player":
                    currentTarget = context.CurrentGameState?.Players?.FirstOrDefault(p => p.PlayerId == context.RequestingPlayerId);
                    break;
                case "opponent":
                    currentTarget = context.CurrentGameState?.Players?.FirstOrDefault(p => p.PlayerId != context.RequestingPlayerId);
                    break;
                case "self": // Refers to the card currently being evaluated in the filter
                    currentTarget = currentItem;
                    break;
                case "gamestate":
                    currentTarget = context.CurrentGameState;
                    break;
                case "triggeringevent": // Placeholder - needs event structure
                    currentTarget = context.TriggeringEvent;
                    _logger.LogWarning("Resolving 'triggeringEvent' path is not fully implemented.");
                    break;
                case "triggeringcard": // Placeholder - needs event structure
                                       // Example: Assuming event has SourceCardInstance property
                                       // currentTarget = (context.TriggeringEvent as dynamic)?.SourceCardInstance; // Unsafe, needs proper event types
                    _logger.LogWarning("Resolving 'triggeringCard' path is not fully implemented.");
                    break;
                // Add more root objects as needed (e.g., specific targets from context)
                default:
                    _logger.LogWarning("Unknown root object in dynamic path: {RootObject}", parts[0]);
                    return null;
            }

            if (currentTarget == null)
            {
                _logger.LogWarning("Root object '{RootObject}' resolved to null for path: {Path}", parts[0], path);
                return null;
            }

            // Traverse the remaining parts of the path
            for (int i = 1; i < parts.Length; i++)
            {
                propertyName = parts[i];
                if (currentTarget == null)
                {
                    _logger.LogWarning("Intermediate object is null when resolving property '{PropertyName}' in path: {Path}", propertyName, path);
                    return null;
                }

                var propertyInfo = currentTarget.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (propertyInfo == null)
                {
                    // Handle special cases like List.Count
                    if (propertyName.Equals("Count", StringComparison.OrdinalIgnoreCase) && currentTarget is System.Collections.ICollection collection)
                    {
                        currentTarget = collection.Count;
                        continue; // Move to next part or finish if this is the last part
                    }

                    _logger.LogWarning("Property '{PropertyName}' not found on type '{TypeName}' for path: {Path}", propertyName, currentTarget.GetType().Name, path);
                    return null;
                }
                currentTarget = propertyInfo.GetValue(currentTarget);
            }

            return currentTarget; // The final value after traversing the path
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

        // TODO: Rename ApplySortOperation to ApplySortOperation (singular) - Renaming deferred for now to minimize diff complexity
        private async Task<List<object>> ApplySortOperation(List<object> pool, SortOperation sortOp, QueryContext context) // Changed signature to async Task
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
                    var key = await ResolveValueSource(orderingConfig.ValueSource, context, item);
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
        private async Task<List<object>> ApplySelectionOperationAsync(List<object> pool, SelectionOperation selectionOp, QueryContext context)
        {
            _logger.LogTrace("Applying selection operation: {Strategy}. CountSource provided: {HasCountSource}",
                selectionOp.Strategy, selectionOp.CountSource != null); // Removed direct Count access

            // --- Resolve Count ---
            int count = 1; // Default count if CountSource is null or resolves incorrectly
            if (selectionOp.CountSource != null)
            {
                // Added await here
                object? resolvedCountRaw = await ResolveValueSource(selectionOp.CountSource, context);
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

        // TODO: Rename ApplyLimitOperation to ApplyLimitOperationOperation (singular) - Renaming deferred
        // ApplyLimitOperation method removed as its functionality is merged into ApplySelectionOperation with SelectionStrategy.Take

        /// <summary>
        /// Resolves a path expression (e.g., ["player", "deck"]) against the query context
        /// to retrieve the corresponding collection of items (cards, definitions, etc.).
        /// </summary>
        /// <param name="path">The list of strings representing the path segments.</param>
        /// <param name="context">The query context containing game state.</param>
        /// <returns>An enumerable collection of objects found at the specified path, or null if not found or invalid.</returns>
        private async Task<IEnumerable<object>?> ResolveSourcePathAsync(List<string> path, QueryContext context)
        {
            _logger.LogTrace("Resolving source path: {Path}", string.Join(".", path));

            if (path == null || !path.Any())
            {
                _logger.LogWarning("Source path is null or empty.");
                return null;
            }

            object? currentTarget = null;
            string rootSegment = path[0].ToLowerInvariant();

            // --- Determine Root Object ---
            switch (rootSegment)
            {
                case "player": // Refers to the requesting player by default
                    currentTarget = context.CurrentGameState?.Players?.FirstOrDefault(p => p.PlayerId == context.RequestingPlayerId);
                    if (currentTarget == null) _logger.LogWarning("Requesting player state not found for path starting with 'player'.");
                    break;
                case "opponent":
                    currentTarget = context.CurrentGameState?.Players?.FirstOrDefault(p => p.PlayerId != context.RequestingPlayerId);
                    if (currentTarget == null) _logger.LogWarning("Opponent player state not found for path starting with 'opponent'.");
                    break;
                case "gamestate":
                    currentTarget = context.CurrentGameState;
                    if (currentTarget == null) _logger.LogWarning("CurrentGameState is null in QueryContext.");
                    break;
                case "allcards": // Special case: Access repository for all definitions
                    try
                    {
                        var definitions = await _cardRepository.GetAllCardDefinitionsAsync();
                        // If path is just "allcards", return the definitions
                        if (path.Count == 1)
                        {
                            _logger.LogDebug("Resolved path 'allcards' to all card definitions ({Count}).", definitions.Count());
                            return definitions.Cast<object>();
                        }
                        else
                        {
                            // Allow further path segments if needed (e.g., "allcards.where(type=follower)") - Requires more complex handling later
                            _logger.LogWarning("Path segments after 'allcards' are not yet supported. Path: {Path}", string.Join(".", path));
                            return null;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error retrieving all card definitions from repository.");
                        return null;
                    }
                // Add cases for specific player IDs if needed, e.g., "player[id]" syntax or similar
                // Add cases for "triggeringevent", "triggeringcard" if context supports them
                default:
                    _logger.LogWarning("Unknown root segment in source path: {RootSegment}", rootSegment);
                    return null;
            }

            if (currentTarget == null)
            {
                // Warning already logged in the switch statement
                return null;
            }

            // --- Traverse Remaining Path Segments ---
            for (int i = 1; i < path.Count; i++)
            {
                string propertyName = path[i];
                if (currentTarget == null) // Should not happen if initial check passed, but safety first
                {
                    _logger.LogWarning("Intermediate target became null before resolving segment '{Segment}' in path: {Path}", propertyName, string.Join(".", path));
                    return null;
                }

                // Use reflection to get the property value
                var propertyInfo = currentTarget.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (propertyInfo == null)
                {
                    _logger.LogWarning("Property '{PropertyName}' not found on type '{TypeName}' for path segment {SegmentIndex} in path: {Path}",
                        propertyName, currentTarget.GetType().Name, i, string.Join(".", path));
                    return null;
                }

                try
                {
                    currentTarget = propertyInfo.GetValue(currentTarget);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting value for property '{PropertyName}' on type '{TypeName}' for path: {Path}",
                        propertyName, currentTarget.GetType().Name, string.Join(".", path));
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
        private async Task<List<object>> ApplyPostProcessingOperationAsync(List<object> pool, PostProcessingOperation postProcessingOp, QueryContext context)
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
                                var definition = await _cardRepository.GetCardDefinitionByIdAsync(cardId.Value); // Correct method name
                                if (definition != null)
                                {
                                    resultPool.Add(MapToCardDefinitionDto(definition));
                                    _logger.LogTrace("Successfully converted item {SourceDescription} to CardDefinitionDto (CardId: {CardId})", sourceDescription, cardId.Value); // Log CardId
                                }
                                else
                                {
                                    _logger.LogWarning("Card definition not found in repository for ID: {CardId}, originating from {SourceDescription}.", cardId.Value, sourceDescription);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error retrieving card definition for ID: {CardId} from repository, originating from {SourceDescription}.", cardId.Value, sourceDescription);
                                // Decide whether to skip or add a placeholder/null
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
                            resultPool.Add(MapToCardInstanceDto(instance)); // Use updated mapping method
                            _logger.LogTrace("Converted CardInstance (InstanceId: {InstanceId}) to CardInstanceDto.", instance.InstanceId);
                        }
                        else
                        {
                            _logger.LogWarning("Item of type {ItemType} cannot be converted to CardInstanceDto. Keeping original item.", item?.GetType().Name ?? "null");
                            resultPool.Add(item); // Keep original if not convertible
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

        // --- Placeholder Mapping Helpers ---
        // TODO: Replace these with actual mapping logic (e.g., AutoMapper or dedicated mapping methods)
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
                // Required properties missing in CardDefinition entity - providing placeholders
                // TODO: Add Rarity and CardSet to CardDefinition entity or handle mapping differently
                Rarity = "Unknown", // Placeholder
                CardSet = "Unknown" // Placeholder
            };
        }

        private CardInstanceDto MapToCardInstanceDto(CardInstance instance)
        {
            // Use the existing ToDto method in CardInstance for basic mapping
            // and then potentially add more details if needed.
            var dto = instance.ToDto();

            // Example: If CardInstanceDto needs properties not set by ToDto(), set them here.
            // dto.SomeOtherProperty = instance.SomeOtherSourceProperty;

            // Note: The ToDto method already handles InstanceId (string to Guid) and
            // maps DefinitionId to CardId. It also maps basic stats and state.
            // We rely on ToDto() to perform the core conversion.

            return dto;

            /* // Old manual mapping - replaced by using instance.ToDto()
            Guid.TryParse(instance.InstanceId, out Guid instanceGuid); // Handle string to Guid conversion

            return new CardInstanceDto
            {
                InstanceId = instanceGuid,
                CardId = instance.DefinitionId, // Correct property name
                // Map other properties from CardInstanceDto based on CardInstance
                // Need to fetch CardDefinition for Name, base Cost, base Attack/Health, Type etc.
                // This mapping is incomplete without CardDefinition lookup.
                // Using instance.ToDto() is generally better if it provides enough info.

                // Instance State:
                CurrentAttack = instance.CurrentAttack,
                CurrentHealth = instance.CurrentHealth,
                MaxHealth = instance.MaxHealth, // Added MaxHealth
                IsEvolved = instance.IsEvolved,
                CanAttack = instance.CanAttack,
                CanEvolve = instance.CanEvolve, // Assuming CanEvolve state exists in CardInstance
                AttacksMadeThisTurn = instance.AttacksThisTurn, // Added AttacksMadeThisTurn
                Zone = instance.CurrentZone, // Correct property name and type
                ControllerPlayerId = instance.OwnerPlayerId, // Map OwnerPlayerId
                CurrentCountdown = instance.Countdown, // Added Countdown

                // Definition-based properties (Requires lookup - not ideal here)
                // Name = ???,
                // Cost = ???, // instance.CurrentCost might differ
                // Type = instance.CardType,
                // Attack = ???,
                // Health = ???,
                // Description = ???,
                // Keywords = ???,
                // EffectDescriptions = ???,
                // MaxAttacksPerTurn = ???,
                // MaxAttack = ???,
            };
            */
        }
        // --- End of Helper Methods ---

    } // Close class CardQueryExecutor
} // Close namespace ShadowverseBeyond.Server.Domain.Query