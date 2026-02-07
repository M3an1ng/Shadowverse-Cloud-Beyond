using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters // Corrected namespace
{
    /// <summary>
    /// Parameters for the Evolve effect. Currently empty as the target is the primary input.
    /// </summary>
    [EffectMetadata("进化", "使目标随从进化，获得进化后的属性和能力。使用卡牌目标。")]
    public class EvolveEffectParams : BaseEffectParams // Corrected base class
    {
        // No specific parameters needed for the basic Evolve effect itself.
        // The target CardInstance is provided via the GameContext.
    }
}
