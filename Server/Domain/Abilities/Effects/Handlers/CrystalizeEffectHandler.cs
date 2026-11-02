using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;
namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.Crystalize)]
    public class CrystalizeEffectHandler : EffectHandlerBase<CrystalizeEffectParams>
    {
        public CrystalizeEffectHandler(ILogger<CrystalizeEffectHandler> logger) : base(logger) { }
        // Declaration only. PlayModeResolver/PlayCardEvent own payment and the alternate card's abilities.
        public override GameStateActionResult Apply(Effect effect, GameContext context, CrystalizeEffectParams parameters)
            => new() { Success = true };
    }
}
