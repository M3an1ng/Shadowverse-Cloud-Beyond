using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services
{
    public class EffectHandlerRegistry : IEffectHandlerRegistry
    {
        private readonly ILogger<EffectHandlerRegistry> _logger;
        private readonly Dictionary<EffectType, (Type handlerType, Type paramsType)> _registry;

        public EffectHandlerRegistry(ILogger<EffectHandlerRegistry> logger)
        {
            _logger = logger;
            _registry = new Dictionary<EffectType, (Type handlerType, Type paramsType)>();
            Initialize();
        }

        private void Initialize()
        {
            _logger.LogInformation("Initializing EffectHandlerRegistry by scanning assemblies...");

            var handlerInterfaceType = typeof(IEffectHandler<>);

            var handlerTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterfaceType));

            foreach (var handlerType in handlerTypes)
            {
                var attribute = handlerType.GetCustomAttribute<EffectHandlerAttribute>();
                if (attribute == null)
                {
                    _logger.LogWarning("Effect handler type {HandlerName} is missing the [EffectHandler] attribute. It will be ignored.", handlerType.Name);
                    continue;
                }

                var genericInterface = handlerType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterfaceType);
                if (genericInterface == null) continue; // Should not happen due to the Where clause, but good for safety

                var paramsType = genericInterface.GetGenericArguments()[0];
                var effectType = attribute.EffectType;

                if (_registry.ContainsKey(effectType))
                {
                    _logger.LogWarning("Duplicate effect handler registration for EffectType.{EffectType}. Existing: {ExistingHandler}, New: {NewHandler}. The new handler will be ignored.",
                        effectType, _registry[effectType].handlerType.Name, handlerType.Name);
                    continue;
                }

                _registry.Add(effectType, (handlerType, paramsType));
                _logger.LogDebug("Registered effect handler: {EffectType} -> {HandlerName} with params {ParamsName}", effectType, handlerType.Name, paramsType.Name);
            }

            _logger.LogInformation("EffectHandlerRegistry initialization complete. Found {Count} handlers.", _registry.Count);
        }

        private IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                var loaded = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
                _logger.LogDebug(ex, "Skipped {Count} unloadable types while scanning assembly {AssemblyName}.", ex.Types.Length - loaded.Length, assembly.FullName);
                return loaded;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to scan types for assembly {AssemblyName}.", assembly.FullName);
                return Array.Empty<Type>();
            }
        }

        public bool TryGetHandler(EffectType effectType, out (Type handlerType, Type paramsType)? registration)
        {
            if (_registry.TryGetValue(effectType, out var foundRegistration))
            {
                registration = foundRegistration;
                return true;
            }

            registration = null;
            return false;
        }
    }
}
