using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;
namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.Accelerate)]
    public class AccelerateEffectHandler : EffectHandlerBase<AccelerateEffectParams>
    {
        public AccelerateEffectHandler(ILogger<AccelerateEffectHandler> logger) : base(logger) { }
        // Declaration only. PlayModeResolver/PlayCardEvent own payment and the alternate card's abilities.
        public override GameStateActionResult Apply(Effect effect, GameContext context, AccelerateEffectParams parameters)
            => new() { Success = true };
    }
}
