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

        // Registry to map effect type strings to Handler and Parameter types
        private static readonly Dictionary<string, (Type handlerType, Type paramsType)> _effectHandlerRegistry = InitializeRegistry();

        public EffectExecutor(
            IServiceProvider serviceProvider,
            IValueSourceResolver valueSourceResolver,
            ILogger<EffectExecutor> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _valueSourceResolver = valueSourceResolver ?? throw new ArgumentNullException(nameof(valueSourceResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private static Dictionary<string, (Type handlerType, Type paramsType)> InitializeRegistry()
        {
            // Use case-insensitive keys by default
            // TODO: 由string改为枚举类
            var registry = new Dictionary<string, (Type handlerType, Type paramsType)>(StringComparer.OrdinalIgnoreCase)
            {
                // Register existing handlers
                { "damage", (typeof(DamageEffectHandler), typeof(DamageEffectParams)) },
                { "heal", (typeof(HealEffectHandler), typeof(HealEffectParams)) },
                { "drawcard", (typeof(DrawCardEffectHandler), typeof(DrawCardEffectParams)) },
                { "modifystats", (typeof(ModifyStatsEffectHandler), typeof(ModifyStatsEffectParams)) },
                { "summon", (typeof(SummonEffectHandler), typeof(SummonUnitEffectParams)) },
                { "destroy", (typeof(DestroyEffectHandler), typeof(DestroyUnitEffectParams)) },
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
                { "turnstart", (typeof(TurnStartEffectHandler), typeof(TurnStartEffectParams)) }
            };

            // Add other handlers here as they are created...

            return registry;
        }


        /// <inheritdoc />
        public async Task<List<IGameEvent>> ExecuteEffectAsync(EffectConfig effectConfig, GameContext context)
        {
            _logger.LogDebug("Executing effect {EffectType} for ability on card {SourceCardId}",
                effectConfig.Type, context.SourceCard?.InstanceId.ToString() ?? "N/A");

            if (context == null)
            {
                _logger.LogError("AbilityContext cannot be null for effect execution.");
                return new List<IGameEvent>();
            }

            try
            {
                // 1. Resolve parameter values using IValueSourceResolver (now using GameContext directly)
                var resolvedParameters = await ResolveEffectParameters(effectConfig, context);

                // 2. Determine the correct handler and parameter type based on effectConfig.Type
                // 3. Create and populate the strongly-typed parameter object (TParams)
                // 4. Resolve the specific IEffectHandler<TParams> using IServiceProvider
                GameStateActionResult actionResult; // Declare actionResult outside the if block

                // 2. Find Handler and Parameter types from registry
                if (_effectHandlerRegistry.TryGetValue(effectConfig.Type, out var registration))
                {
                    var (handlerType, paramsType) = registration;

                    // 3. Create Parameter instance
                    var paramsObject = Activator.CreateInstance(paramsType);
                    if (paramsObject == null)
                    {
                        _logger.LogError("Failed to create parameter object instance for type {ParamsType}", paramsType.Name);
                        return new List<IGameEvent>(); // Or throw?
                    }

                    // 4. Populate Parameter instance from resolved values
                    PopulateParameters(paramsObject, paramsType, resolvedParameters);

                    // 5. Resolve Handler instance from DI container
                    var handlerInstance = _serviceProvider.GetRequiredService(handlerType);
                    if (handlerInstance == null)
                    {
                        _logger.LogError("Failed to resolve handler instance for type {HandlerType}", handlerType.Name);
                        return new List<IGameEvent>(); // Or throw?
                    }

                    // 6. Get the correct ApplyAsync(GameContext, TParams) method
                    // We need the specific parameter type (paramsType) for the signature
                    var applyMethod = handlerType.GetMethod("ApplyAsync", new[] { typeof(GameContext), paramsType });
                    if (applyMethod == null)
                    {
                        // Fallback: Check if it inherits from EffectHandlerBase and try getting the ExecuteAsync(TParams, GameContext)
                        // This might be less direct but could work if ApplyAsync isn't directly found (e.g., explicit interface implementation)
                        // However, the primary target should be ApplyAsync as defined in IEffectHandler<TParams>
                        _logger.LogError("Could not find ApplyAsync(GameContext, {ParamsTypeName}) method on handler type {HandlerType}", paramsType.Name, handlerType.Name);
                        // Attempt to find the base class ExecuteAsync as a less ideal fallback - THIS IS LIKELY WRONG for direct call
                        // var baseExecuteMethod = handlerType.GetMethod("ExecuteAsync", new[] { paramsType, typeof(GameContext) });
                        // if (baseExecuteMethod == null) {
                        //    _logger.LogError("Also could not find ExecuteAsync({ParamsTypeName}, GameContext) method on handler type {HandlerType}", paramsType.Name, handlerType.Name);
                        //    return new List<IGameEvent>(); // Or throw?
                        // }
                        // applyMethod = baseExecuteMethod; // Use fallback if found - RECONSIDER THIS

                        // For now, strictly require ApplyAsync
                        return new List<IGameEvent>(); // Or throw?
                    }

                    // 7. Invoke the ApplyAsync method
                    var task = applyMethod.Invoke(handlerInstance, new object[] { context, paramsObject }) as Task<GameStateActionResult>;
                    if (task == null)
                    {
                        _logger.LogError("ApplyAsync method on {HandlerType} did not return a Task<GameStateActionResult>.", handlerType.Name);
                        return new List<IGameEvent>(); // Or throw?
                    }

                    actionResult = await task; // Await the task to get the result
                }
                else
                {
                    _logger.LogWarning("No handler registered for effect type '{EffectType}'. Effect execution skipped.", effectConfig.Type);
                    actionResult = new GameStateActionResult { Success = false, ErrorMessage = $"Unsupported effect type: {effectConfig.Type}" };
                }


                // 7. Process the result (GameStateActionResult already contains events)
                if (!actionResult.Success)
                {
                    _logger.LogError("Effect handler for {EffectType} failed: {ErrorMessage}", effectConfig.Type, actionResult.ErrorMessage);
                    // Optionally handle specific errors or return specific events
                }

                _logger.LogDebug("Effect {EffectType} generated {EventCount} events.", effectConfig.Type, actionResult.GeneratedEvents?.Count ?? 0);
                // Return the events directly as the type is now List<IGameEvent>
                return actionResult.GeneratedEvents ?? new List<IGameEvent>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing effect {EffectType} for ability on card {SourceCardId}",
                    effectConfig.Type, context.SourceCard?.InstanceId.ToString() ?? "N/A");
                return new List<IGameEvent>();
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
    }
} // End of namespace ShadowverseBeyond.Server.Domain.Abilities.Effects