using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata
{
    /// <summary>
    /// Associates an Effect Handler class with a specific EffectType enum.
    /// This provides a direct mapping for the metadata service to link a game-logic enum to its implementation and its corresponding EffectParams class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class EffectHandlerAttribute : Attribute
    {
        /// <summary>
        /// The EffectType that this handler is responsible for.
        /// </summary>
        public EffectType EffectType { get; }

        public EffectHandlerAttribute(EffectType effectType)
        {
            EffectType = effectType;
        }
    }
}