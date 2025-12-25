using System;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services
{
    /// <summary>
    /// Provides a centralized registry for discovering and retrieving effect handler and parameter types.
    /// This service decouples the EffectExecutor from the concrete handler implementations.
    /// </summary>
    public interface IEffectHandlerRegistry
    {
        /// <summary>
        /// Attempts to get the registered handler and parameter types for a given EffectType.
        /// </summary>
        /// <param name="effectType">The effect type to look up.</param>
        /// <param name="registration">When this method returns, contains the registration tuple if found; otherwise, null.</param>
        /// <returns>true if a registration is found; otherwise, false.</returns>
        bool TryGetHandler(EffectType effectType, out (Type handlerType, Type paramsType)? registration);
    }
}