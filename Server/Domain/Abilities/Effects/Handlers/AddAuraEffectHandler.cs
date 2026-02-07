using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.AddAura)]
    public class AddAuraEffectHandler : EffectHandlerBase<AddAuraEffectParams>
    {
        public AddAuraEffectHandler(ILogger<AddAuraEffectHandler> logger)
            : base(logger)
        {
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, AddAuraEffectParams parameters)
        {
            const string effectName = "AddAuraEffect";
            Logger.LogWarning("{EffectName} is deprecated. Use AuraConfig on abilities instead.", effectName);
            return new GameStateActionResult
            {
                Success = false,
                ErrorMessage = $"{effectName} is deprecated. Use AuraConfig on abilities instead."
            };
        }
    }
}
