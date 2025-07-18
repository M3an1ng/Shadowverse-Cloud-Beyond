using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection; // Added for GetRequiredService
using ShadowverseBeyond.Server.Domain.Abilities.Common; // For ValueConfig
using ShadowverseBeyond.Server.Domain.Abilities.Runtime; // For Runtime classes if needed (though AbilityContext is primary)
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For TargetId
using ShadowverseBeyond.Server.Domain.Entities; // For CardInstance, GameState
using ShadowverseBeyond.Server.Domain.Events;   // For IGameEvent
using ShadowverseBeyond.Server.Domain.Constants; // Add using for CardKeyword enum
using ShadowverseBeyond.Server.Dtos; // For GameStateDto, PlayerStateDto etc.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection; // Added for reflection
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers; // Added for Handler types
// using ShadowverseBeyond.Server.Domain.Abilities.Effects.Parameters; // REMOVED - No longer needed
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters; // Keep this for ALL parameter types now
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases; // Added for EffectHandlerBase
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Processing; // For DslExpressionParser
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation; // For DslEvaluator
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST; // For ExpressionNode

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
{
    /// <summary>
    /// Default implementation of IEffectExecutor.
    /// Handles the execution of a single effect, resolving parameters and dispatching to the appropriate IEffectHandler<TParams>.
    /// </summary>
    public class EffectExecutor : IEffectExecutor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IValueSourceResolver _valueSourceResolver;
        private readonly ILogger<EffectExecutor> _logger;
        private readonly DslExpressionParser _dslExpressionParser;
        private readonly DslEvaluator _dslEvaluator;

        // Registry to map effect type strings to Handler and Parameter types
        private static readonly Dictionary<string, (Type handlerType, Type paramsType)> _effectHandlerRegistry = InitializeRegistry();

        public EffectExecutor(
            IServiceProvider serviceProvider,
            IValueSourceResolver valueSourceResolver,
            ILogger<EffectExecutor> logger,
            DslExpressionParser dslExpressionParser,
            DslEvaluator dslEvaluator)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _valueSourceResolver = valueSourceResolver ?? throw new ArgumentNullException(nameof(valueSourceResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dslExpressionParser = dslExpressionParser ?? throw new ArgumentNullException(nameof(dslExpressionParser));
            _dslEvaluator = dslEvaluator ?? throw new ArgumentNullException(nameof(dslEvaluator));
        }

        private static Dictionary<string, (Type handlerType, Type paramsType)> InitializeRegistry()
        {
            // Use case-insensitive keys by default
            // TODO: 由string改为枚举类,应该想办法默认注册所有实现的能力
            var registry = new Dictionary<string, (Type handlerType, Type paramsType)>(StringComparer.OrdinalIgnoreCase)
            {
                // Register existing handlers
                { "damage", (typeof(DamageEffectHandler), typeof(DamageEffectParams)) },
                { "heal", (typeof(HealEffectHandler), typeof(HealEffectParams)) },
                { "drawcard", (typeof(DrawCardEffectHandler), typeof(DrawCardEffectParams)) },
                { "modifystats", (typeof(ModifyStatsEffectHandler), typeof(ModifyStatsEffectParams)) },
                { "summon", (typeof(SummonEffectHandler), typeof(SummonEffectParams)) },
                { "destroy", (typeof(DestroyEffectHandler), typeof(DestroyEffectParams)) },
                { "addkeyword", (typeof(AddKeywordEffectHandler), typeof(AddKeywordEffectParams)) },
                { "removekeyword", (typeof(RemoveKeywordEffectHandler), typeof(RemoveKeywordEffectParams)) },

                // Register newly migrated handlers
                { "addcardtohand", (typeof(AddCardToHandEffectHandler), typeof(AddCardToHandEffectParams)) },
                { "modifypp", (typeof(ModifyPPEffectHandler), typeof(ModifyPPEffectParams)) },
                { "modifyep", (typeof(ModifyEPEffectHandler), typeof(ModifyEPEffectParams)) },
                { "modifyppmax", (typeof(ModifyPPMaxEffectHandler), typeof(ModifyPPMaxEffectParams)) },
                { "evolve", (typeof(EvolveEffectHandler), typeof(EvolveEffectParams)) },
                { "transform", (typeof(TransformEffectHandler), typeof(TransformEffectParams)) },

                // Register system operation handlers
                { "playcard", (typeof(PlayCardEffectHandler), typeof(PlayCardEffectParams)) },
                { "attack", (typeof(AttackEffectHandler), typeof(AttackEffectParams)) },
                { "turnend", (typeof(TurnEndEffectHandler), typeof(TurnEndEffectParams)) },
                { "mulligan", (typeof(MulliganEffectHandler), typeof(MulliganEffectParams)) },
                { "turnstart", (typeof(TurnStartEffectHandler), typeof(TurnStartEffectParams)) },
            };

            // Add other handlers here as they are created...

            return registry;
        }




        /// <summary>
        /// 【架构重构】执行单个效果并返回完整的结果，包括事件和效果请求
        /// </summary>
        public async Task<GameStateActionResult> ExecuteEffectAsync(EffectConfig effectConfig, GameContext context)
        {
            _logger.LogDebug("🔧 [EffectExecutor] Executing effect {EffectType} with full result for ability on card {SourceCardId}",
                effectConfig.Type, context.SourceCard?.InstanceId.ToString() ?? "N/A");

            if (context == null)
            {
                _logger.LogError("AbilityContext cannot be null for effect execution.");
                return new GameStateActionResult { Success = false, ErrorMessage = "AbilityContext cannot be null." };
            }

            try
            {
                // 1. Resolve parameter values using IValueSourceResolver
                var resolvedParameters = await ResolveEffectParameters(effectConfig, context);

                // 2. Determine the correct handler and parameter type based on effectConfig.Type
                GameStateActionResult actionResult;

                // Find Handler and Parameter types from registry
                _logger.LogDebug("🔧 [EffectExecutor] Looking for handler for effect type '{EffectType}'", effectConfig.Type);
                if (_effectHandlerRegistry.TryGetValue(effectConfig.Type, out var registration))
                {
                    var (handlerType, paramsType) = registration;
                    _logger.LogDebug("🔧 [EffectExecutor] Found handler {HandlerType} with params {ParamsType}", handlerType.Name, paramsType.Name);

                    // 3. Create Parameter instance
                    var paramsObject = Activator.CreateInstance(paramsType);
                    if (paramsObject == null)
                    {
                        _logger.LogError("Failed to create parameter object instance for type {ParamsType}", paramsType.Name);
                        return new GameStateActionResult { Success = false, ErrorMessage = $"Failed to create parameter object for {paramsType.Name}" };
                    }

                    // 4. Populate Parameter instance from resolved values
                    PopulateParameters(paramsObject, paramsType, resolvedParameters);

                    // 5. Resolve Handler instance from DI container
                    var handlerInstance = _serviceProvider.GetRequiredService(handlerType);
                    if (handlerInstance == null)
                    {
                        _logger.LogError("Failed to resolve handler instance for type {HandlerType}", handlerType.Name);
                        return new GameStateActionResult { Success = false, ErrorMessage = $"Failed to resolve handler {handlerType.Name}" };
                    }

                    // 6. Get the correct ApplyAsync(GameContext, TParams) method
                    var applyMethod = handlerType.GetMethod("ApplyAsync", new[] { typeof(GameContext), paramsType });
                    if (applyMethod == null)
                    {
                        _logger.LogError("Could not find ApplyAsync(GameContext, {ParamsTypeName}) method on handler type {HandlerType}", paramsType.Name, handlerType.Name);
                        return new GameStateActionResult { Success = false, ErrorMessage = $"ApplyAsync method not found on {handlerType.Name}" };
                    }

                    // 7. Invoke the ApplyAsync method
                    var task = applyMethod.Invoke(handlerInstance, new object[] { context, paramsObject }) as Task<GameStateActionResult>;
                    if (task == null)
                    {
                        _logger.LogError("ApplyAsync method on {HandlerType} did not return a Task<GameStateActionResult>.", handlerType.Name);
                        return new GameStateActionResult { Success = false, ErrorMessage = $"Invalid return type from {handlerType.Name}" };
                    }

                    _logger.LogDebug("🔧 [EffectExecutor] About to invoke handler method...");
                    actionResult = await task; // Await the task to get the result
                    _logger.LogDebug("🔧 [EffectExecutor] Handler execution completed. Success: {Success}, Events: {EventCount}",
                        actionResult.Success, actionResult.GeneratedEvents?.Count ?? 0);
                }
                else
                {
                    _logger.LogWarning("🔧 [EffectExecutor] No handler registered for effect type '{EffectType}'. Available types: [{AvailableTypes}]",
                        effectConfig.Type, string.Join(", ", _effectHandlerRegistry.Keys));
                    actionResult = new GameStateActionResult { Success = false, ErrorMessage = $"Unsupported effect type: {effectConfig.Type}" };
                }

                // 8. Process the result from handler
                if (!actionResult.Success)
                {
                    _logger.LogError("Effect handler for {EffectType} failed: {ErrorMessage}", effectConfig.Type, actionResult.ErrorMessage);
                    // Even if handler failed, we might still want to generate an EffectCompletedEvent if Outputs are defined.
                    // Or, we could return early. For now, let's proceed to generate EffectCompletedEvent.
                }

                // 9. Resolve Outputs and Create EffectCompletedEvent
                var calculatedOutputs = new Dictionary<string, object?>();
                if (effectConfig.Outputs != null && effectConfig.Outputs.Any())
                {
                    _logger.LogDebug("🔧 [EffectExecutor] Resolving {OutputCount} outputs for effect {EffectType}", effectConfig.Outputs.Count, effectConfig.Type);
                    // Prepare context for output evaluation
                    context.SetTemporaryVariable("inputParameters", resolvedParameters); // resolvedParameters is Dictionary<string, object>
                    context.SetTemporaryVariable("handlerResults", actionResult.Results); // List<DomainOperationResult>
                    // context.GeneratedEvents already contains events from the handler

                    foreach (var outputEntry in effectConfig.Outputs)
                    {
                        var outputName = outputEntry.Key;
                        var dslExpression = outputEntry.Value;
                        if (string.IsNullOrWhiteSpace(dslExpression))
                        {
                            calculatedOutputs[outputName] = null;
                            continue;
                        }

                        var astNode = _dslExpressionParser.ParseExpression(dslExpression);
                        if (astNode != null)
                        {
                            var evalResult = await _dslEvaluator.EvaluateAsync(astNode, context);
                            if (evalResult.IsSuccess)
                            {
                                calculatedOutputs[outputName] = evalResult.Value;
                            }
                            else
                            {
                                _logger.LogWarning("Failed to evaluate output DSL '{DslExpression}' for output '{OutputName}'. Errors: {Errors}", dslExpression, outputName, string.Join(", ", evalResult.Errors));
                                calculatedOutputs[outputName] = null; // Or some error indicator
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Failed to parse output DSL '{DslExpression}' for output '{OutputName}'. Parser errors exist.", dslExpression, outputName);
                            calculatedOutputs[outputName] = null; // Or some error indicator
                        }
                    }
                }

                // Create EffectCompletedEvent
                // var originalIntendedEventType = MapEffectTypeToEventType(effectConfig.Type); // Removed: No longer mapping to EventType
                var abilityTag = context.CurrentAbility?.Config.AbilityTag ?? effectConfig.Id ?? string.Empty;

                var effectCompletedEvent = new EffectCompletedEvent(
                    effectConfig.Type, // Pass the original effect type string directly
                    resolvedParameters, // These are the input parameters to the effect handler
                    context.SelectedTargets.ToList().AsReadOnly(), // Targets selected before handler execution
                    calculatedOutputs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value).AsReadOnly(),
                    context.RequestingPlayerId,
                    context.SourceCard,
                    null // sourceAbilityInstanceId (Guid?) - Ability.AbilityId is long
                );

                actionResult.GeneratedEvents ??= new List<IGameEvent>();
                actionResult.GeneratedEvents.Add(effectCompletedEvent);
                _logger.LogDebug("🔧 [EffectExecutor] Added EffectCompletedEvent for {EffectType}", effectConfig.Type);


                // 【关键修复】在Effect执行完毕后清理GameContext的临时状态
                context.ClearTemporaryState(); // This will clear inputParameters and handlerResults as well
                _logger.LogDebug("🔧 [EffectExecutor] Cleared GameContext temporary state after executing {EffectType}", effectConfig.Type);

                _logger.LogDebug("🔧 [EffectExecutor] Effect {EffectType} generated {EventCount} events (including EffectCompletedEvent).",
                    effectConfig.Type, actionResult.GeneratedEvents?.Count ?? 0);

                return actionResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing effect {EffectType} for ability on card {SourceCardId}",
                    effectConfig.Type, context.SourceCard?.InstanceId.ToString() ?? "N/A");
                return new GameStateActionResult { Success = false, ErrorMessage = ex.Message };
            }
        }



        // Helper method to resolve parameters
        private async Task<Dictionary<string, object>> ResolveEffectParameters(
            EffectConfig effectConfig,
            GameContext gameContext)
        {
            var resolvedParams = new Dictionary<string, object>();

            foreach (var paramKvp in effectConfig.Parameters)
            {
                if (paramKvp.Value is ValueConfig valueConfig)
                {
                    try
                    {
                        // Use the correct ResolveAsync method from IValueSourceResolver
                        var resolvedValue = await _valueSourceResolver.ResolveAsync(valueConfig, gameContext);
                        resolvedParams[paramKvp.Key] = resolvedValue;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error resolving parameter '{ParamKey}' using ValueConfig for effect {EffectType}", paramKvp.Key, effectConfig.Type);
                        // Decide on fallback behavior: throw, use default, or skip parameter?
                        // Using null for now, might need adjustment.
                        resolvedParams[paramKvp.Key] = null!;
                    }
                }
                else
                {
                    // If not a ValueConfig, treat it as a static value
                    resolvedParams[paramKvp.Key] = paramKvp.Value;
                }
            }
            return resolvedParams;
        }

        // Helper method to populate a parameter object from a dictionary using reflection
        private void PopulateParameters(object paramsObject, Type paramsType, Dictionary<string, object> resolvedParameters)
        {
            foreach (var kvp in resolvedParameters)
            {
                var property = paramsType.GetProperty(kvp.Key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.CanWrite)
                {
                    try
                    {
                        // Attempt direct assignment first
                        var value = kvp.Value;

                        // 【修复】优先处理null值，特别是Guid?等可空类型
                        if (value == null)
                        {
                            if (IsNullableType(property.PropertyType))
                            {
                                property.SetValue(paramsObject, null);
                                continue;
                            }
                            else
                            {
                                // Skip setting non-nullable properties to null
                                _logger.LogWarning("Attempted to set non-nullable property {PropertyName} to null. Skipping.", property.Name);
                                continue;
                            }
                        }

                        // Handle potential type mismatches (e.g., int vs long, string vs enum)
                        if (value != null && property.PropertyType != value.GetType())
                        {
                            // Specific handling for enums stored as strings
                            if (property.PropertyType.IsEnum && value is string stringValue)
                            {
                                try
                                {
                                    value = Enum.Parse(property.PropertyType, stringValue, true); // Case-insensitive enum parsing
                                }
                                catch (ArgumentException ex)
                                {
                                    _logger.LogWarning(ex, "Failed to parse enum value '{StringValue}' for property {PropertyName} of type {EnumType}. Skipping.", stringValue, property.Name, property.PropertyType.Name);
                                    continue; // Skip this property if parsing fails
                                }
                            }
                            // Handle potential integer type mismatches (e.g., config uses int, property expects long or vice versa)
                            else if (IsIntegerType(property.PropertyType) && IsIntegerType(value.GetType()))
                            {
                                try
                                {
                                    value = Convert.ChangeType(value, property.PropertyType);
                                }
                                catch (OverflowException ex)
                                {
                                    _logger.LogWarning(ex, "Integer conversion overflow for property {PropertyName} from value {Value} to type {TargetType}. Skipping.", property.Name, value, property.PropertyType.Name);
                                    continue;
                                }
                                catch (InvalidCastException ex)
                                {
                                    _logger.LogWarning(ex, "Invalid integer cast for property {PropertyName} from value {Value} to type {TargetType}. Skipping.", property.Name, value, property.PropertyType.Name);
                                    continue;
                                }
                            }
                            // Add more specific type conversions if needed (e.g., string to Guid)
                            else
                            {
                                // Attempt a general conversion for other types
                                try
                                {
                                    value = Convert.ChangeType(value, property.PropertyType);
                                }
                                catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is OverflowException)
                                {
                                    _logger.LogWarning(ex, "Failed to convert value '{Value}' of type {ValueType} to property {PropertyName} of type {PropertyType}. Skipping.", kvp.Value, kvp.Value?.GetType().Name ?? "null", property.Name, property.PropertyType.Name);
                                    continue; // Skip if general conversion fails
                                }
                            }
                        }

                        property.SetValue(paramsObject, value);
                    }
                    catch (Exception ex) // Catch potential exceptions during SetValue
                    {
                        _logger.LogError(ex, "Error setting property {PropertyName} on type {ParamsTypeName} with value '{Value}'.", property.Name, paramsType.Name, kvp.Value);
                        // Decide if we should continue or re-throw depending on desired strictness
                    }
                }
                else if (property == null)
                {
                    _logger.LogWarning("Property '{PropertyName}' not found (case-insensitive) on parameter type {ParamsTypeName}. Skipping.", kvp.Key, paramsType.Name);
                }
                // else: Property found but cannot write (e.g., no setter), which is less common for DTO-like params
            }
        }

        // Helper to check if a type is an integer type
        private static bool IsIntegerType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
                   type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte);
        }

        // Helper to check if a type is nullable
        private static bool IsNullableType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        // Removed MapEffectTypeToEventType method as it's no longer needed.
    }
} // End of namespace ShadowverseBeyond.Server.Domain.Abilities.Effects