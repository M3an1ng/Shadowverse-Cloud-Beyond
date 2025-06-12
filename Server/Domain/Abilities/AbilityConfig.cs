
//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
//

// Removed: using ShadowverseBeyond.Server.Domain.Query.Operations;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
// Removed alias: using TargetsNS = ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// Represents the configuration for a card ability, potentially parsed from DSL.
    /// </summary>
    public class AbilityConfig
    {
        public string Id { get; set; } = string.Empty;
        public int CardId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AbilityTag { get; set; } = string.Empty;
        public AbilityType Type { get; set; }
        public TriggerConfig Trigger { get; set; } = new();
        /// <summary>
        /// 卡牌打出前的预判断条件。
        /// 只有满足这些条件（以及基础条件如费用、场地），卡牌才能被打出。
        /// </summary>
        public List<ConditionConfig> PreConditions { get; set; } = new();
        public List<ConditionConfig> Conditions { get; set; } = new();
        public List<TargetConfig> Targets { get; set; } = new();
        public List<EffectConfig> Effects { get; set; } = new();
        public bool IsActive { get; set; } = true;
        public string Version { get; set; } = "1.0";
        public string? DslSource { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public DSL.AST.AbilityNode? CompiledAst { get; set; }

        public Dictionary<string, object> Metadata { get; set; } = new();

        [System.Text.Json.Serialization.JsonIgnore]
        public CardInstance? SourceCard { get; set; }

        // --- Static Factory Methods ---

        public static AbilityConfig CreateTriggered(string tag, string eventType, List<EffectConfig> effects)
        {
            if (!Enum.TryParse<GameEventType>(eventType, true, out var gameEventTypeEnum))
            {
                throw new ArgumentException($"Invalid GameEventType string provided: {eventType}", nameof(eventType));
            }
            return new AbilityConfig
            {
                Id = Guid.NewGuid().ToString(),
                AbilityTag = tag,
                Type = AbilityType.Triggered,
                Trigger = TriggerConfig.OnEvent(gameEventTypeEnum),
                Effects = effects ?? new List<EffectConfig>()
            };
        }

        public static AbilityConfig CreateKeyword(string keyword, List<EffectConfig> effects)
        {
            return new AbilityConfig
            {
                Id = Guid.NewGuid().ToString(),
                AbilityTag = keyword,
                Type = AbilityType.Keyword,
                Effects = effects ?? new List<EffectConfig>()
            };
        }

        public static AbilityConfig CreateContinuous(string tag, List<EffectConfig> effects)
        {
            return new AbilityConfig
            {
                Id = Guid.NewGuid().ToString(),
                AbilityTag = tag,
                Type = AbilityType.Continuous,
                Effects = effects ?? new List<EffectConfig>()
            };
        }

        /// <summary>
        /// Creates an AbilityConfig from DSL source code.
        /// </summary>
        public static AbilityConfig FromDsl(string dslSource)
        {
            if (string.IsNullOrWhiteSpace(dslSource))
                throw new ArgumentException("DSL source cannot be null or whitespace.", nameof(dslSource));

            var lexer = new Lexer(dslSource);
            var tokens = lexer.Tokenize();
            // TODO: Handle lexer errors

            var parser = new Parser(tokens);
            var ast = parser.ParseAbility();

            if (parser.Errors.Any())
            {
                Console.Error.WriteLine($"DSL Parsing failed for source:\n{dslSource}");
                var errorMessages = parser.Errors.Select(e => e.ToString()).ToList();
                foreach (var error in errorMessages) Console.Error.WriteLine(error);
                throw new AggregateException($"DSL Parsing failed with {parser.Errors.Count} errors.", parser.Errors.Select(e => new ParseException(e.ToString())));
            }

            var config = new AbilityConfig
            {
                Id = Guid.NewGuid().ToString(),
                Name = ast.Name ?? "UnnamedAbility",
                AbilityTag = ast.Tag ?? ast.Name ?? "UnnamedAbility",
                Type = MapAbilityType(ast.Type),
                DslSource = dslSource,
                CompiledAst = ast,
                Conditions = new List<ConditionConfig>(),
                Targets = new List<TargetConfig>(),
                Effects = new List<EffectConfig>(),
                Metadata = new Dictionary<string, object>()
            };

            try
            {
                MapAstToConfig(ast, config);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error mapping AST to Config for ability '{config.Name}': {ex.Message}\n{ex.StackTrace}");
                throw new InvalidOperationException($"Failed to map DSL AST to AbilityConfig for '{config.Name}'. See inner exception.", ex);
            }

            var validation = config.Validate();
            if (!validation.IsValid)
            {
                Console.Error.WriteLine($"Validation failed after mapping DSL for Ability '{config.Name}':");
                foreach (var error in validation.Errors) Console.Error.WriteLine($"- Error: {error}");
                foreach (var warning in validation.Warnings) Console.Error.WriteLine($"- Warning: {warning}");
                throw new InvalidOperationException($"Generated AbilityConfig for '{config.Name}' failed validation: {string.Join("; ", validation.Errors)}");
            }

            return config;
        }

        // --- Instance Methods ---

        public ValidationResult Validate()
        {
            var result = new ValidationResult();
            if (string.IsNullOrEmpty(Id)) result.AddError("Ability ID cannot be empty");
            if (string.IsNullOrEmpty(AbilityTag)) result.AddError("Ability tag cannot be empty");
            if (Type == AbilityType.Triggered && (Trigger == null || Trigger.EventTypes == null || Trigger.EventTypes.Count == 0))
                result.AddError("Triggered ability must have a valid TriggerConfig with at least one event type");
            if (Effects == null) result.AddError("Effects list cannot be null.");
            else if (Effects.Count == 0 && Type != AbilityType.Keyword) result.AddWarning("Ability has no effects defined");
            else if (Effects.Any(e => e == null)) result.AddError("Ability Effects list contains null entries");
            if (Targets == null) result.AddError("Targets list cannot be null.");
            else if (Targets.Any(t => t == null)) result.AddError("Ability Targets list contains null entries");
            if (Conditions == null) result.AddError("Conditions list cannot be null.");
            else if (Conditions.Any(c => c == null)) result.AddError("Ability Conditions list contains null entries");
            return result;
        }

        /// <summary>
        /// Creates a shallow clone. Needs proper deep cloning implementation.
        /// </summary>
        public AbilityConfig Clone()
        {
            var clone = (AbilityConfig)this.MemberwiseClone();
            clone.Conditions = this.Conditions != null ? new List<ConditionConfig>(this.Conditions) : new List<ConditionConfig>();
            clone.Targets = this.Targets != null ? new List<TargetConfig>(this.Targets) : new List<TargetConfig>();
            clone.Effects = this.Effects != null ? new List<EffectConfig>(this.Effects) : new List<EffectConfig>();
            clone.Metadata = this.Metadata != null ? new Dictionary<string, object>(this.Metadata) : new Dictionary<string, object>();
            // clone.Trigger = this.Trigger?.Clone(); // Requires TriggerConfig.Clone()
            // clone.CompiledAst = this.CompiledAst?.Clone(); // Requires AST nodes to be cloneable
            return clone;
        }

        // --- Private Static Helper Methods for DSL Mapping ---

        private static AbilityType MapAbilityType(string dslType)
        {
            return dslType.ToLowerInvariant() switch
            {
                "triggered" => AbilityType.Triggered,
                "keyword" => AbilityType.Keyword,
                "continuous" => AbilityType.Continuous,
                "activated" => AbilityType.Activated,
                _ => throw new ArgumentException($"Unknown DSL ability type: {dslType}")
            };
        }

        private static void MapAstToConfig(DSL.AST.AbilityNode ast, AbilityConfig config)
        {
            if (ast.Trigger != null) config.Trigger = MapTriggerNode(ast.Trigger);
            else if (config.Type == AbilityType.Triggered) config.Trigger = new TriggerConfig();

            config.Conditions = ast.Conditions?.Select(MapConditionNode).ToList() ?? new List<ConditionConfig>();
            config.Targets = ast.Targets?.Select(MapTargetNode).ToList() ?? new List<TargetConfig>();
            config.Effects = ast.Effects?.Select(MapEffectNode).ToList() ?? new List<EffectConfig>(); // Restored call

            foreach (var prop in ast.Properties ?? Enumerable.Empty<PropertyNode>())
            {
                // Add null check for prop.Value before accessing it
                var propValue = prop.Value;
                config.Metadata[prop.Name] = EvaluateAstLiteral(propValue) ?? propValue?.ToString() ?? string.Empty; // Added null check and default
            }
        }

        private static TriggerConfig MapTriggerNode(DSL.AST.TriggerNode node)
        {
            var triggerConfig = new TriggerConfig();
            // Add null check for node.EventTypes
            if (node.EventTypes != null)
            {
                foreach (var eventTypeStr in node.EventTypes)
                {
                    // Ensure eventTypeStr is not null before parsing
                    if (string.IsNullOrEmpty(eventTypeStr)) continue;

                    if (Enum.TryParse<GameEventType>(eventTypeStr, true, out var gameEventTypeEnum))
                        triggerConfig.EventTypes.Add(gameEventTypeEnum);
                    else
                        Console.Error.WriteLine($"Warning: Invalid GameEventType '{eventTypeStr}' in DSL trigger.");
                }
            }
            if (node.Condition != null)
            {
                Console.WriteLine($"Info: Trigger condition found in DSL but TriggerConfig mapping not fully implemented: {node.Condition}");
                // TODO: Map node.Condition to TriggerConfig if extended
            }
            // TODO: Map Scope, Timing, IsOptional if TriggerConfig supports them
            return triggerConfig;
        }

        private static ConditionConfig MapConditionNode(DSL.AST.ConditionNode node)
        {
            // Store the raw DSL expression string in the Expression property.
            return new ConditionConfig
            {
                // Add null check for node.Expression
                Expression = node.Expression?.ToString() ?? string.Empty
            };
        }

        private static TargetConfig MapTargetNode(DSL.AST.TargetNode node)
        {
            // Use the correct namespace for the interface
            var queryOperations = new List<Query.IQueryOperation>();

            // Map Filters
            // Add null check for node.Filters
            if (node.Filters != null && node.Filters.Any())
            {
                // Ensure MapFilterNode handles potential nulls if node.Filters can contain null FilterNode
                var filterConfigs = node.Filters.Where(f => f != null).Select(MapFilterNode).ToList();
                // Use the correct FilterOperation from Query namespace
                if (filterConfigs.Any()) // Only add if there are valid filters
                {
                    queryOperations.Add(new Query.FilterOperation(filterConfigs));
                }
            }

            // Map Count/Limit to SelectionOperation
            // Add null check for node.Count
            var limitValue = node.Count != null ? EvaluateAstExpressionToInt(node.Count) : null;
            var selectionMethod = MapDslSelectionMethod(node.Selection); // Get selection method early

            // Use SelectionOperation for both count and selection strategy
            ValueConfig? countSource = limitValue.HasValue ? ValueConfig.Static(limitValue.Value) : null;
            // Only add SelectionOperation if there's a limit or a non-default selection strategy
            // Compare selectionMethod (Query.SelectionStrategy) with the corresponding default value in its own enum
            if (countSource != null || selectionMethod != Query.SelectionStrategy.First) // Check against the default we mapped 'all' to
            {
                // Use the correct SelectionOperation from Query namespace
                queryOperations.Add(new ShadowverseBeyond.Server.Domain.Query.SelectionOperation(
                    Strategy: selectionMethod, // Map DSL selection to SelectionStrategy enum
                    CountSource: countSource
                ));
            }

            // Map Ordering (if needed, DSL currently doesn't explicitly support complex sorting)
            // If DSL adds sorting, map it to SortOperation here.
            // Example:
            // if (node.Sortings != null && node.Sortings.Any())
            // {
            //     var orderingConfigs = node.Sortings.Select(MapSortingNode).ToList();
            //     queryOperations.Add(new ShadowverseBeyond.Server.Domain.Query.SortOperation(orderingConfigs));
            // }


            // Use fully qualified name to avoid ambiguity
            var queryConfig = new ShadowverseBeyond.Server.Domain.Query.CardQueryConfig
            {
                SourcePoolExpression = MapDslTargetTypeToTargetPool(node.Type, node.Scope),
                Operations = queryOperations
                // TargetType defaults to Instance, adjust if needed based on DSL
            };

            var targetConfig = new TargetConfig
            {
                // SelectorType is set below based on RequiresPlayerChoice
                Query = queryConfig, // Assign the constructed query
                Description = $"DSL Target: Type={node.Type}, Scope={node.Scope}, Selection={node.Selection}",
                // PlayerChoice = node.RequiresPlayerChoice ? new PlayerChoiceConfig { IsRequired = true } : null, // Incorrect: PlayerChoice is not a direct property
                VariableName = null // VariableName is set below if SelectorType is FromVariable
            };

            // Set SelectorType and PlayerChoiceOptions based on DSL node
            if (node.RequiresPlayerChoice)
            {
                targetConfig.SelectorType = Common.SelectorType.PlayerChoice;
                targetConfig.PlayerChoiceOptions = new PlayerChoiceConfig
                {
                    IsRequired = true,
                    // Set Count limits based on the DSL Count value
                    MinCount = limitValue ?? 1, // Use DSL count as minimum, default to 1
                    MaxCount = limitValue, // Use DSL count as maximum, null means no limit
                    AutoSelect = limitValue == 1, // Auto-select if only one target needed
                    CanCancel = false, // Default to non-cancelable for required choices
                    Prompt = $"Choose {(limitValue.HasValue ? limitValue.Value.ToString() : "target(s)")} from available options"
                };
            }
            else
            {
                targetConfig.SelectorType = MapDslTargetTypeToSelectorType(node.Type);
                if (targetConfig.SelectorType == Common.SelectorType.FromVariable)
                {
                    targetConfig.VariableName = node.Type; // Assign variable name if type implies it
                }
            }


            return targetConfig;
        }

        // Use fully qualified name for return type and enum members
        // Remove fully qualified name, rely on using statement
        // Explicitly qualify return type and enum members with Targets namespace
        // Use full namespace to avoid conflict with 'Targets' property
        // Use namespace alias 'TargetsNS' to resolve ambiguity - Removed alias
        private static Common.SelectorType MapDslTargetTypeToSelectorType(string dslType) // Use Common namespace
        {
            // Note: "enemy", "ally", "all" scopes are handled by the SourcePoolExpression
            // in CardQueryConfig. SelectorType indicates the *method* of selection.
            // Mapping them to Self here as a placeholder, the query executor
            // should primarily rely on the CardQueryConfig.
            // TODO: Re-evaluate if a different SelectorType is more appropriate or if this mapping is sufficient.
            // Use fully qualified name for enum members
            // Remove fully qualified name from enum members
            // Explicitly qualify enum members with Targets namespace
            // Use full namespace to avoid conflict with 'Targets' property
            // Use namespace alias 'TargetsNS' to resolve ambiguity - Removed alias
            return dslType.ToLowerInvariant() switch
            {
                "self" => Common.SelectorType.Self,
                "enemy" => Common.SelectorType.Self, // Handled by QueryConfig's SourcePoolExpression
                "ally" => Common.SelectorType.Self, // Handled by QueryConfig's SourcePoolExpression
                "all" => Common.SelectorType.Self,  // Handled by QueryConfig's SourcePoolExpression
                "playerchoice" => Common.SelectorType.PlayerChoice, // This case might be handled earlier by RequiresPlayerChoice flag
                "triggeringeventdata" => Common.SelectorType.TriggeringEventData,
                _ => Common.SelectorType.FromVariable // Assume unknown types might refer to variables
            };
        }

        // Helper to evaluate simple literal AST nodes
        private static object? EvaluateAstLiteral(DSL.AST.ExpressionNode? node) // Allow null node
        {
            // Add null check for node
            if (node is DSL.AST.LiteralNode literal)
            {
                return literal.Value;
            }
            // Add handling for other simple literal types if necessary (e.g., maybe specific nodes for true/false?)
            return null; // Cannot evaluate non-literals simply
        }
        // Helper to evaluate simple AST expression nodes to an integer
        private static int? EvaluateAstExpressionToInt(DSL.AST.ExpressionNode? node) // Allow null node
        {
            // Add null check for node
            if (node == null) return null;

            var literalValue = EvaluateAstLiteral(node);
            if (literalValue is int intValue)
            {
                return intValue;
            }
            if (literalValue is long longValue && longValue >= int.MinValue && longValue <= int.MaxValue)
            {
                return (int)longValue;
            }
            // Add conversion from other numeric types if needed (e.g., double, decimal)
            // Handle simple identifiers or context lookups if required
            Console.Error.WriteLine($"Warning: Cannot evaluate AST node '{node}' to integer directly.");
            return null;
        }
        /// <summary>
        /// Maps a DSL filter operator string to the Query.ComparisonOperator enum.
        /// </summary>
        /// <param name="dslOperator">DSL 操作符字符串</param>
        /// <returns>查询比较操作符枚举 (Query.ComparisonOperator)</returns>
        private static Query.ComparisonOperator MapFilterOperator(string dslOperator) // Changed return type namespace
        {
            // Map to Query.ComparisonOperator
            return dslOperator switch
            {
                "==" => Query.ComparisonOperator.EqualTo,
                "!=" => Query.ComparisonOperator.NotEqualTo,
                ">" => Query.ComparisonOperator.GreaterThan,
                ">=" => Query.ComparisonOperator.GreaterThanOrEqualTo,
                "<" => Query.ComparisonOperator.LessThan,
                "<=" => Query.ComparisonOperator.LessThanOrEqualTo,
                "contains" => Query.ComparisonOperator.Contains, // Assuming Query enum has Contains
                "not contains" => Query.ComparisonOperator.NotContains, // Assuming Query enum has NotContains
                // TODO: Add mapping for other potential operators if Query.ComparisonOperator supports them
                _ => throw new ArgumentException($"Unsupported filter operator: {dslOperator}")
            };
        }

        /// <summary>
        /// Maps a DSL ExpressionNode to a Common.ValueConfig.
        /// </summary>
        /// <param name="expr">AST 表达式节点 (nullable)</param>
        /// <returns>值配置 (Common.ValueConfig)</returns>
        private static ValueConfig MapExpressionToValueConfig(DSL.AST.ExpressionNode? expr) // Allow null expr
        {
            // Add null check for expr
            if (expr == null)
            {
                Console.Error.WriteLine("Warning: Null expression node passed to MapExpressionToValueConfig. Returning default static empty string.");
                return ValueConfig.Static(string.Empty); // Return a default or error config
            }

            switch (expr) // Use Common.ValueConfig factory methods
            {
                case DSL.AST.LiteralNode literal:
                    return ValueConfig.Static(literal.Value);
                case DSL.AST.IdentifierNode identifier:
                    // Map identifier to dynamic source (e.g., variable, context)
                    return ValueConfig.Dynamic(identifier.Name);
                case DSL.AST.BinaryOperationNode binaryExpr: // Corrected typo: BinaryOperationNode
                    // Map binary expression to formula or complex dynamic source
                    // TODO: Implement proper mapping for binary expressions if needed
                    // Use the static factory method
                    return ValueConfig.CreateFormulaValue(binaryExpr.ToString()); // Corrected typo: BinaryOperationNode
                // TODO: Handle UnaryOperationNode, FunctionCallNode etc.
                default:
                    Console.Error.WriteLine($"Warning: Cannot map complex AST expression type '{expr.GetType().Name}' to Common.ValueConfig directly. Storing as formula string.");
                    // Fallback: Treat as formula string, use the static factory method
                    return ValueConfig.CreateFormulaValue(expr.ToString());
            }
        }

        /// <summary>
        /// Maps a DSL FilterNode to a FilterConfig (Query Operation Filter).
        /// </summary>
        /// <param name="node">DSL 过滤器节点</param>
        /// <returns>查询过滤器配置 (Query.FilterConfig)</returns>
        // Assuming this should return Query.FilterConfig based on usage in MapTargetNode
        // and that Query.FilterConfig accepts Common.ValueConfig for sources/targets
        // and Query.ComparisonOperator for the operator.
        private static Query.FilterConfig MapFilterNode(DSL.AST.FilterNode node)
        {
            // Map the property access to ValueSource (Common.ValueConfig)
            var valueSourceConfig = ValueConfig.Dynamic(node.Property); // Assuming property node converts to path string

            // Map the comparison value (Common.ValueConfig)
            // Add null check for node.Value
            var targetValueConfig = node.Value != null
                                        ? MapExpressionToValueConfig(node.Value)
                                        : ValueConfig.Static(null); // Handle null value case

            // Map the operator (Query.ComparisonOperator)
            var queryComparisonOperator = MapFilterOperator(node.Operator); // Use updated helper

            // Assuming Query.FilterConfig has properties like this.
            // Use the constructor as defined in QueryOperations.cs
            return new ShadowverseBeyond.Server.Domain.Query.FilterConfig(
                ValueSource: valueSourceConfig,
                TargetValue: targetValueConfig,
                Operator: queryComparisonOperator
            );
        }

        // Helper to map ExpressionNode to Common.ValueConfig (This seems duplicated, ensure only one exists or rename)
        private static ValueConfig MapExpressionToCommonValueConfig(DSL.AST.ExpressionNode? expr) // Allow null expr
        {
            // Add null check for expr
            if (expr == null)
            {
                Console.Error.WriteLine("Warning: Null expression node passed to MapExpressionToCommonValueConfig. Returning default static empty string.");
                return ValueConfig.Static(string.Empty); // Return a default or error config
            }
            switch (expr)
            {
                case DSL.AST.LiteralNode literal:
                    return ValueConfig.Static(literal.Value); // Use Common.ValueConfig factory
                case DSL.AST.IdentifierNode identifier:
                    // Assuming identifiers map to variables for now
                    return ValueConfig.Variable(identifier.Name); // Use Common.ValueConfig factory
                // TODO: Handle BinaryOperationNode, UnaryOperationNode, FunctionCallNode etc.
                // This would likely involve creating Formula type ValueConfigs or more complex dynamic lookups.
                default:
                    Console.Error.WriteLine($"Warning: Cannot map complex AST expression type '{expr.GetType().Name}' to Common.ValueConfig directly. Storing as static string.");
                    // Fallback: Store string representation as static value
                    return ValueConfig.Static(expr.ToString());
            }
        }

        private static EffectConfig MapEffectNode(DSL.AST.EffectNode node)
        {
            var effectConfig = new EffectConfig
            {
                Type = node.Type,
                // Add null check for node.Target
                Target = node.Target != null ? MapTargetNode(node.Target) : null,
                // Add null check for node.Amount
                Amount = node.Amount != null ? MapExpressionToCommonValueConfig(node.Amount) : ValueConfig.Static(null), // Use default static null
                Parameters = new Dictionary<string, ValueConfig>()
            };

            // Add null check for node.Parameters
            foreach (var param in node.Parameters ?? Enumerable.Empty<ParameterNode>())
            {
                // Add null check for param and param.Value
                if (param != null && param.Value != null)
                {
                    effectConfig.Parameters[param.Name] = MapExpressionToCommonValueConfig(param.Value);
                }
                else if (param != null)
                {
                    // Handle case where parameter exists but value is null, if necessary
                    effectConfig.Parameters[param.Name] = ValueConfig.Static(null);
                }
            }
            // TODO: Map SubEffects, ExecutionOrder

            return effectConfig;
        }

        /// <summary>
        /// Maps a DSL target type string (like "field", "hand") and scope ("self", "opponent")
        /// to a Source Pool Expression string for CardQueryConfig.
        /// </summary>
        /// <param name="dslType">DSL 类型字符串 (e.g., "field", "hand")</param>
        /// <param name="dslScope">DSL 范围字符串 (e.g., "self", "opponent")</param>
        /// <returns>源池表达式字符串</returns>
        private static string MapDslTargetTypeToTargetPool(string dslType, string dslScope) // Changed return type
        {
            // Map scope and type to a SourcePoolExpression string
            // Examples: "Self.Field", "Opponent.Hand", "All.Graveyard", "TriggeringEvent.Targets"
            // Needs a robust mapping logic based on defined SourcePoolExpression syntax
            // TODO: Implement the mapping logic based on the final SourcePoolExpression syntax

            string playerScope = dslScope.ToLowerInvariant() switch
            {
                "self" => "Self",
                "opponent" => "Opponent",
                "enemy" => "Opponent", // Alias
                "ally" => "Self", // Alias
                "all" => "All",
                "trigger" => "TriggeringEvent", // Special scope
                _ => "Self" // Default or throw? Consider context
            };

            string zone = dslType.ToLowerInvariant() switch
            {
                "field" => "Field",
                "hand" => "Hand",
                "deck" => "Deck",
                "graveyard" => "Graveyard",
                "leader" => "Leader",
                "self" => "Context.SourceCard", // Explicit self type overrides scope
                "source" => "Context.SourceCard", // Alias for self type
                "eventtargets" => "Targets", // Assuming Targets within TriggeringEvent
                "triggeringeventdata" => "Targets", // Alias
                _ => "Field" // Default zone or throw?
            };

            // Handle special cases where player scope might not apply or is implicit
            if (zone == "Context.SourceCard")
            {
                return zone; // Scope is irrelevant
            }
            if (playerScope == "TriggeringEvent")
            {
                // Combine TriggeringEvent scope with zone (e.g., TriggeringEvent.Targets)
                return $"{playerScope}.{zone}";
            }


            // Combine scope and zone for standard cases
            return $"{playerScope}.{zone}";
        }

        // Change return type to Query.SelectionStrategy
        private static ShadowverseBeyond.Server.Domain.Query.SelectionStrategy MapDslSelectionMethod(string? dslSelection)
        {
            // Map to Query.SelectionStrategy enum members
            return dslSelection?.ToLowerInvariant() switch
            {
                "random" => ShadowverseBeyond.Server.Domain.Query.SelectionStrategy.Random,
                "first" => ShadowverseBeyond.Server.Domain.Query.SelectionStrategy.First,
                "last" => ShadowverseBeyond.Server.Domain.Query.SelectionStrategy.Last,
                // "all" is not a strategy in Query.SelectionStrategy, it implies no specific selection beyond filters/limit.
                // We handle the absence of a specific strategy in MapTargetNode.
                // Map "all" or null/empty to a default that signifies no specific selection needed by SelectionOperation itself.
                // However, SelectionOperation requires a strategy. Let's default to 'First' if 'all' or null is specified,
                // as 'First' is often the implicit default when no other strategy is given.
                // The actual "select all" behavior comes from *not* adding a LimitOperation or using SelectionOperation with Count=All (if supported).
                // Revisit this default if needed.
                "all" => ShadowverseBeyond.Server.Domain.Query.SelectionStrategy.First, // Default placeholder
                _ => ShadowverseBeyond.Server.Domain.Query.SelectionStrategy.First // Default or throw? Defaulting to First.
            };
        }


        // Placeholder for ValidationResult if not defined elsewhere
        public class ValidationResult
        {
            public bool IsValid => !Errors.Any();
            public List<string> Errors { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();

            public void AddError(string message) => Errors.Add(message);
            public void AddWarning(string message) => Warnings.Add(message);
        }
    } // End of AbilityConfig class
} // End of namespace