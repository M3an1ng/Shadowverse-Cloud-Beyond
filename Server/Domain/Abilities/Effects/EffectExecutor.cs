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
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation; // For DslEvaluator
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue; // For ExpressionNode
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;

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
        private readonly IEffectHandlerRegistry _effectHandlerRegistry;

        public EffectExecutor(
            IServiceProvider serviceProvider,
            IValueSourceResolver valueSourceResolver,
            ILogger<EffectExecutor> logger,
            IEffectHandlerRegistry effectHandlerRegistry)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _valueSourceResolver = valueSourceResolver ?? throw new ArgumentNullException(nameof(valueSourceResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _effectHandlerRegistry = effectHandlerRegistry ?? throw new ArgumentNullException(nameof(effectHandlerRegistry));
        }




        /// <summary>
        /// 【架构重构】执行单个效果并返回完整的结果，包括事件和效果请求
        /// </summary>
        public GameStateActionResult ExecuteEffect(Effect effect, GameContext context)
        {
            var effectConfig = effect.EffectConfig;
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
                var resolvedParameters = ResolveEffectParameters(effectConfig, context);

                // 2. Determine the correct handler and parameter type based on effectConfig.Type
                GameStateActionResult actionResult;

                // Find Handler and Parameter types from registry
                _logger.LogDebug("🔧 [EffectExecutor] Looking for handler for effect type '{EffectType}'", effectConfig.Type);
                if (_effectHandlerRegistry.TryGetHandler(effectConfig.Type, out var registration) && registration.HasValue)
                {
                    var (handlerType, paramsType) = registration.Value;
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

                    // 6. Get the correct Apply(GameContext, TParams) method
                    // 6. Get the correct Apply(Effect, GameContext, TParams) method
                    var applyMethod = handlerType.GetMethod("Apply", new[] { typeof(Effect), typeof(GameContext), paramsType });
                    if (applyMethod == null)
                    {
                        _logger.LogError("Could not find Apply(Effect, GameContext, {ParamsTypeName}) method on handler type {HandlerType}", paramsType.Name, handlerType.Name);
                        return new GameStateActionResult { Success = false, ErrorMessage = $"Apply method not found on {handlerType.Name}" };
                    }

                    // 7. Invoke the Apply method
                    var result = applyMethod.Invoke(handlerInstance, new object[] { effect, context, paramsObject });
                    if (result is not GameStateActionResult)
                    {
                        _logger.LogError("Apply method on {HandlerType} did not return a GameStateActionResult.", handlerType.Name);
                        return new GameStateActionResult { Success = false, ErrorMessage = $"Invalid return type from {handlerType.Name}" };
                    }

                    _logger.LogDebug("🔧 [EffectExecutor] About to invoke handler method...");
                    actionResult = (GameStateActionResult)result;
                    _logger.LogDebug("🔧 [EffectExecutor] Handler execution completed. Success: {Success}, Events: {EventCount}",
                        actionResult.Success, actionResult.GeneratedEvents?.Count ?? 0);
                }
                else
                {
                    _logger.LogWarning("🔧 [EffectExecutor] No handler registered for effect type '{EffectType}'. Available types: [{AvailableTypes}]",
                        effectConfig.Type, "N/A"); // We can't easily list all keys from the new registry
                    actionResult = new GameStateActionResult { Success = false, ErrorMessage = $"Unsupported effect type: {effectConfig.Type}" };
                }

                // 8. Process the result from handler and populate the effect object with generated events
                // This makes the events available for subsequent DSL evaluation via context.CurrentEffect.GeneratedEvents
                if (actionResult.GeneratedEvents != null)
                {
                    effect.GeneratedEvents = actionResult.GeneratedEvents;
                }
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

                        // Delegate the resolution of the formula to the ValueSourceResolver
                        var valueConfig = new FormulaValueConfig { Formula = dslExpression };
                        var resolvedValue = _valueSourceResolver.Resolve(valueConfig, context);
                        calculatedOutputs[outputName] = resolvedValue;
                    }
                }

                // Built-in outputs for certain effect types (so downstream chained abilities can rely on them).
                if (effectConfig.Type == EffectType.Fusion)
                {
                    var fusedIds = effect.SelectedTargets
                        .Where(t => t.Card != null)
                        .Select(t => t.Card!.InstanceId)
                        .ToList();

                    if (!calculatedOutputs.ContainsKey("fusedCount"))
                        calculatedOutputs["fusedCount"] = fusedIds.Count;
                    if (!calculatedOutputs.ContainsKey("fusedIds"))
                        calculatedOutputs["fusedIds"] = fusedIds;
                }

                // Create EffectCompletedEvent
                var effectCompletedEvent = new EffectCompletedEvent(
                    effectConfig.Type,
                    resolvedParameters, // These are the input parameters to the effect handler
                    effect.SelectedTargets.AsReadOnly(), // Get targets directly from the Effect DTO
                    calculatedOutputs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value).AsReadOnly(),
                    context.RequestingPlayerId,
                    context.SourceCard,
                    null // sourceAbilityInstanceId (Guid?) - Ability.AbilityId is long
                );

                actionResult.GeneratedEvents ??= new List<IGameEvent>();
                actionResult.GeneratedEvents.Add(effectCompletedEvent);
                _logger.LogDebug("🔧 [EffectExecutor] Added EffectCompletedEvent for {EffectType}", effectConfig.Type);


                // 【关键修复】在Effect执行完毕后清理GameContext的临时状态

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
        private Dictionary<string, object> ResolveEffectParameters(
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
                        // Use the correct Resolve method from IValueSourceResolver
                        var resolvedValue = _valueSourceResolver.Resolve(valueConfig, gameContext);
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
                                // Attempt a general conversion for other types, with special handling for nullables
                                try
                                {
                                    var targetType = property.PropertyType;
                                    // Check if the target type is a nullable type (e.g., int?, Guid?)
                                    var underlyingType = Nullable.GetUnderlyingType(targetType);

                                    if (underlyingType != null)
                                    {
                                        // It's a nullable type. Convert the value to the underlying non-nullable type first.
                                        value = Convert.ChangeType(value, underlyingType);
                                    }
                                    else
                                    {
                                        // It's not a nullable type, so convert directly to the property's type.
                                        value = Convert.ChangeType(value, targetType);
                                    }
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
