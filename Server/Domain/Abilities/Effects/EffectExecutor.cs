using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.JsonConverters;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Logging;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using Microsoft.AspNetCore.SignalR;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Hubs.ClientInterfaces;
using ShadowverseBeyond.Server.Services.Abstractions;

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
            if (effect == null)
            {
                _logger.LogError("Effect cannot be null for effect execution.");
                return new GameStateActionResult { Success = false, ErrorMessage = "Effect cannot be null." };
            }

            if (context == null)
            {
                _logger.LogError("AbilityContext cannot be null for effect execution.");
                return new GameStateActionResult { Success = false, ErrorMessage = "AbilityContext cannot be null." };
            }

            var effectConfig = effect.EffectConfig;
            _logger.LogDebug("🔧 [EffectExecutor] Executing effect {EffectType} with full result for ability on card {SourceCardId}",
                effectConfig.Type, context.SourceCard?.InstanceId.ToString() ?? "N/A");

            var resolvedParameters = new Dictionary<string, object>();
            var calculatedOutputs = new Dictionary<string, object?>();
            var actionResult = new GameStateActionResult();

            try
            {
                // 1. Resolve parameter values using IValueSourceResolver
                resolvedParameters = ResolveEffectParameters(effect, context);

                // 2. Determine the correct handler and parameter type based on effectConfig.Type
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
                        actionResult = new GameStateActionResult { Success = false, ErrorMessage = $"Failed to create parameter object for {paramsType.Name}" };
                    }
                    else
                    {
                        // 4. Populate Parameter instance from resolved values
                        PopulateParameters(paramsObject, paramsType, resolvedParameters);

                        // 5. Resolve Handler instance from DI container
                        var handlerInstance = _serviceProvider.GetRequiredService(handlerType);
                        if (handlerInstance == null)
                        {
                            _logger.LogError("Failed to resolve handler instance for type {HandlerType}", handlerType.Name);
                            actionResult = new GameStateActionResult { Success = false, ErrorMessage = $"Failed to resolve handler {handlerType.Name}" };
                        }
                        else
                        {
                            // 6. Get the correct Apply(GameContext, TParams) method
                            // 6. Get the correct Apply(Effect, GameContext, TParams) method
                            var applyMethod = handlerType.GetMethod("Apply", new[] { typeof(Effect), typeof(GameContext), paramsType });
                            if (applyMethod == null)
                            {
                                _logger.LogError("Could not find Apply(Effect, GameContext, {ParamsTypeName}) method on handler type {HandlerType}", paramsType.Name, handlerType.Name);
                                actionResult = new GameStateActionResult { Success = false, ErrorMessage = $"Apply method not found on {handlerType.Name}" };
                            }
                            else
                            {
                                // 7. Invoke the Apply method
                                var result = applyMethod.Invoke(handlerInstance, new object[] { effect, context, paramsObject });
                                if (result is not GameStateActionResult)
                                {
                                    _logger.LogError("Apply method on {HandlerType} did not return a GameStateActionResult.", handlerType.Name);
                                    actionResult = new GameStateActionResult { Success = false, ErrorMessage = $"Invalid return type from {handlerType.Name}" };
                                }
                                else
                                {
                                    _logger.LogDebug("🔧 [EffectExecutor] About to invoke handler method...");
                                    actionResult = (GameStateActionResult)result;
                                    _logger.LogDebug("🔧 [EffectExecutor] Handler execution completed. Success: {Success}, Events: {EventCount}",
                                        actionResult.Success, actionResult.GeneratedEvents?.Count ?? 0);
                                }
                            }
                        }
                    }

                }
                else
                {
                    _logger.LogWarning("🔧 [EffectExecutor] No handler registered for effect type '{EffectType}'. Available types: [{AvailableTypes}]",
                        effectConfig.Type, "N/A"); // We can't easily list all keys from the new registry
                    actionResult = new GameStateActionResult { Success = false, ErrorMessage = $"Unsupported effect type: {effectConfig.Type}" };
                }

                // 8. Process the result from handler and populate the effect object with generated events
                // This makes the events available for subsequent DSL evaluation via context.CurrentEffect.GeneratedEvents
                var mergedEvents = new List<IGameEvent>();
                var mergedEventIds = new HashSet<Guid>();
                if (effect.GeneratedEvents != null && effect.GeneratedEvents.Count > 0)
                {
                    foreach (var existingEvent in effect.GeneratedEvents)
                    {
                        if (mergedEventIds.Add(existingEvent.EventId))
                        {
                            mergedEvents.Add(existingEvent);
                        }
                    }
                }
                if (actionResult.GeneratedEvents != null && actionResult.GeneratedEvents.Count > 0)
                {
                    foreach (var newEvent in actionResult.GeneratedEvents)
                    {
                        if (mergedEventIds.Add(newEvent.EventId))
                        {
                            mergedEvents.Add(newEvent);
                        }
                    }
                }
                actionResult.GeneratedEvents = mergedEvents;
                effect.GeneratedEvents = mergedEvents;
                if (!actionResult.Success)
                {
                    _logger.LogError("Effect handler for {EffectType} failed: {ErrorMessage}", effectConfig.Type, actionResult.ErrorMessage);
                    // EffectCompletedEvent is materialized by the transaction service after
                    // generated events are fully processed.
                }

                // 9. Resolve Outputs (EffectCompletedEvent is created in transaction service
                // after generated events finish recursive processing)
                calculatedOutputs = new Dictionary<string, object?>();
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

                // Always expose execution success/failure for downstream DSL checks.
                calculatedOutputs["effectFailed"] = !actionResult.Success;

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

                actionResult.ResolvedParameters = resolvedParameters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value).AsReadOnly();
                actionResult.Outputs = calculatedOutputs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value).AsReadOnly();

                _logger.LogDebug("🔧 [EffectExecutor] Effect {EffectType} generated {EventCount} events.",
                    effectConfig.Type, actionResult.GeneratedEvents?.Count ?? 0);

                return actionResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing effect {EffectType} for ability on card {SourceCardId}",
                    effectConfig.Type, context.SourceCard?.InstanceId.ToString() ?? "N/A");
                actionResult.Success = false;
                if (string.IsNullOrWhiteSpace(actionResult.ErrorMessage))
                {
                    actionResult.ErrorMessage = ex.Message;
                }

                var mergedEvents = new List<IGameEvent>();
                var mergedEventIds = new HashSet<Guid>();
                if (effect.GeneratedEvents != null && effect.GeneratedEvents.Count > 0)
                {
                    foreach (var existingEvent in effect.GeneratedEvents)
                    {
                        if (mergedEventIds.Add(existingEvent.EventId))
                        {
                            mergedEvents.Add(existingEvent);
                        }
                    }
                }
                if (actionResult.GeneratedEvents != null && actionResult.GeneratedEvents.Count > 0)
                {
                    foreach (var newEvent in actionResult.GeneratedEvents)
                    {
                        if (mergedEventIds.Add(newEvent.EventId))
                        {
                            mergedEvents.Add(newEvent);
                        }
                    }
                }

                var generatedEvents = mergedEvents
                    .Where(e => e is not EffectCompletedEvent)
                    .ToList();

                actionResult.GeneratedEvents = generatedEvents;
                effect.GeneratedEvents = generatedEvents;

                var fallbackOutputs = new Dictionary<string, object?>(calculatedOutputs);
                fallbackOutputs["effectFailed"] = true;
                actionResult.ResolvedParameters = resolvedParameters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value).AsReadOnly();
                actionResult.Outputs = fallbackOutputs.AsReadOnly();
                return actionResult;
            }
        }

        // Helper method to resolve parameters
        private Dictionary<string, object> ResolveEffectParameters(
            Effect effect,
            GameContext gameContext)
        {
            var effectConfig = effect.EffectConfig;
            var resolvedParams = new Dictionary<string, object>();
            var traceSettings = _serviceProvider.GetService<IConditionTraceSettings>();
            var traceEntries = traceSettings?.IsEffectEnabledFor(gameContext) == true ? new List<ConditionTraceEntryDto>() : null;

            foreach (var paramKvp in effectConfig.Parameters)
            {
                if (effectConfig.Type == EffectType.Draw
                    && string.Equals(paramKvp.Key, "SearchTargets", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedParams[paramKvp.Key] = paramKvp.Value;
                    continue;
                }

                if (paramKvp.Value is ValueConfig valueConfig)
                {
                    try
                    {
                        if (traceEntries != null && valueConfig is FormulaValueConfig formulaConfig)
                        {
                            var traceCollector = new DslTraceCollector();
                            var resolvedValue = _valueSourceResolver.ResolveFormulaValueWithTrace(formulaConfig.Formula, gameContext, traceCollector);
                            var traceOutput = DslTraceFormatter.Format(formulaConfig.Formula, traceCollector);
                            traceEntries.Add(new ConditionTraceEntryDto
                            {
                                Expression = formulaConfig.Formula,
                                Trace = traceOutput
                            });
                            resolvedParams[paramKvp.Key] = resolvedValue!;
                        }
                        else
                        {
                            // Use the correct Resolve method from IValueSourceResolver
                            var resolvedValue = _valueSourceResolver.Resolve(valueConfig, gameContext);
                            resolvedParams[paramKvp.Key] = resolvedValue;
                        }
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

            if (traceEntries != null)
            {
                var ability = effect.SourceAbility;
                var effectTypeLabel = effectConfig.Type.ToString();
                var message = $"能力 {ability?.Name ?? "UnknownAbility"} 的效果参数解析完成（效果类型：{effectTypeLabel}）。";
                SendEffectTrace(gameContext, ability, traceEntries, effectTypeLabel, message);
            }

            return resolvedParams;
        }

        private void SendEffectTrace(
            GameContext context,
            IAbility? ability,
            List<ConditionTraceEntryDto> entries,
            string effectType,
            string message)
        {
            var traceSettings = _serviceProvider.GetService<IConditionTraceSettings>();
            var hubContext = _serviceProvider.GetService<IHubContext<GameHub, IGameClient>>();
            if (traceSettings == null || hubContext == null || context.CurrentGameState == null)
            {
                return;
            }
            var matchService = _serviceProvider.GetService<IMatchService>();
            var roomScopedId = matchService != null
                && matchService.TryGetRoomIdByGameId(context.CurrentGameState.GameId, out var roomId)
                && !string.IsNullOrWhiteSpace(roomId)
                    ? roomId
                    : context.CurrentGameState.GameId;

            var sourceCard = ability?.SourceCard;
            var cardName = sourceCard?.Name ?? "UnknownCard";
            var cardId = sourceCard?.InstanceId.ToString() ?? "UnknownId";

            var playerIds = new[]
            {
                context.CurrentGameState.Player1?.PlayerId,
                context.CurrentGameState.Player2?.PlayerId
            };

            foreach (var playerId in playerIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct())
            {
                if (playerId == Guid.Empty || !traceSettings.IsEffectEnabled(playerId))
                {
                    continue;
                }

                var dto = new ConditionTraceFailedDto
                {
                    GameId = context.CurrentGameState.GameId,
                    AbilityRuntimeId = ability?.RuntimeId ?? Guid.Empty,
                    AbilityName = ability?.Name ?? string.Empty,
                    CardName = cardName,
                    CardId = cardId,
                    Success = true,
                    TraceKind = "Effect",
                    EffectType = effectType,
                    ConditionId = string.Empty,
                    Expression = entries.Count > 0 ? entries[0].Expression : string.Empty,
                    Trace = entries.Count > 0 ? entries[0].Trace : string.Empty,
                    Conditions = entries,
                    Message = message
                };

                _ = hubContext.Clients.Group(SignalRGroupNames.Player(roomScopedId, playerId)).ConditionTraceSucceeded(dto);
            }
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

                        if (value is JsonElement jsonElement && property.PropertyType != typeof(JsonElement))
                        {
                            try
                            {
                                var options = new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                };
                                options.Converters.Add(new LenientEffectKeywordConverter());
                                options.Converters.Add(new JsonStringEnumConverter());

                                var deserialized = JsonSerializer.Deserialize(
                                    jsonElement.GetRawText(),
                                    property.PropertyType,
                                    options);
                                value = deserialized;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to deserialize JsonElement for property {PropertyName} to type {PropertyType}.", property.Name, property.PropertyType.Name);
                            }
                        }

                        // Handle null values first, especially for nullable property types.
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

                        if (property.PropertyType == typeof(object))
                        {
                            property.SetValue(paramsObject, value);
                            continue;
                        }

                        if (TryConvertToList(property.PropertyType, value, out var listValue))
                        {
                            property.SetValue(paramsObject, listValue);
                            continue;
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
                else if (!TrySetNestedDictionaryParameter(paramsObject, paramsType, kvp.Key, kvp.Value))
                {
                    if (property == null)
                    {
                        _logger.LogWarning("Property '{PropertyName}' not found (case-insensitive) on parameter type {ParamsTypeName}. Skipping.", kvp.Key, paramsType.Name);
                    }
                }
                // else: Property found but cannot write (e.g., no setter), which is less common for DTO-like params
            }
        }

        private bool TrySetNestedDictionaryParameter(object paramsObject, Type paramsType, string key, object? value)
        {
            var dotIndex = key.IndexOf('.');
            if (dotIndex <= 0 || dotIndex >= key.Length - 1)
            {
                return false;
            }

            var rootName = key.Substring(0, dotIndex);
            var subKey = key[(dotIndex + 1)..];

            var property = paramsType.GetProperty(rootName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite)
            {
                return false;
            }

            if (!typeof(global::System.Collections.IDictionary).IsAssignableFrom(property.PropertyType))
            {
                return false;
            }

            var current = property.GetValue(paramsObject) as global::System.Collections.IDictionary;
            if (current == null)
            {
                if (property.PropertyType.IsInterface || property.PropertyType.IsAbstract)
                {
                    current = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    try
                    {
                        current = (global::System.Collections.IDictionary)Activator.CreateInstance(property.PropertyType)!;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to create dictionary instance for property {PropertyName}.", property.Name);
                        return false;
                    }
                }

                property.SetValue(paramsObject, current);
            }

            current[subKey] = value;
            return true;
        }

        // Helper to check if a type is an integer type
        private static bool IsIntegerType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
                   type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte);
        }

        private static bool TryConvertToList(Type targetType, object value, out object? result)
        {
            result = null;

            if (!targetType.IsGenericType || targetType.GetGenericTypeDefinition() != typeof(List<>))
            {
                return false;
            }

            var elementType = targetType.GetGenericArguments()[0];
            if (elementType != typeof(int))
            {
                return false;
            }

            if (value is IEnumerable<int> intEnumerable)
            {
                result = intEnumerable.ToList();
                return true;
            }

            if (IsIntegerType(value.GetType()))
            {
                result = new List<int> { Convert.ToInt32(value) };
                return true;
            }

            if (value is double doubleValue)
            {
                if (Math.Abs(doubleValue % 1) < double.Epsilon)
                {
                    result = new List<int> { Convert.ToInt32(doubleValue) };
                    return true;
                }
            }

            if (value is float floatValue)
            {
                if (Math.Abs(floatValue % 1) < float.Epsilon)
                {
                    result = new List<int> { Convert.ToInt32(floatValue) };
                    return true;
                }
            }

            if (value is decimal decimalValue)
            {
                if (decimalValue == decimal.Truncate(decimalValue))
                {
                    result = new List<int> { Convert.ToInt32(decimalValue) };
                    return true;
                }
            }

            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Array)
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<List<int>>(element.GetRawText());
                        result = parsed ?? new List<int>();
                        return true;
                    }
                    catch
                    {
                        result = new List<int>();
                        return true;
                    }
                }

                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
                {
                    result = new List<int> { number };
                    return true;
                }

                if (element.ValueKind == JsonValueKind.String)
                {
                    result = ParseDelimitedIntList(element.GetString() ?? string.Empty);
                    return true;
                }
            }

            if (value is string text)
            {
                result = ParseDelimitedIntList(text);
                return true;
            }

            if (value is global::System.Collections.IEnumerable enumerable)
            {
                var list = new List<int>();
                foreach (var item in enumerable)
                {
                    if (item == null) continue;
                    if (IsIntegerType(item.GetType()))
                    {
                        list.Add(Convert.ToInt32(item));
                        continue;
                    }
                    if (item is string itemText && int.TryParse(itemText, out var parsed))
                    {
                        list.Add(parsed);
                    }
                }
                result = list;
                return true;
            }

            return false;
        }

        private static List<int> ParseDelimitedIntList(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<int>();
            }

            return text
                .Split(new[] { ',', '，', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .Select(item => int.TryParse(item, out var parsed) ? parsed : (int?)null)
                .Where(item => item.HasValue)
                .Select(item => item!.Value)
                .ToList();
        }

        // Helper to check if a type is nullable
        private static bool IsNullableType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        // Removed MapEffectTypeToEventType method as it's no longer needed.
    }
} // End of namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
