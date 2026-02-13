using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.PreventEvent)]
    public class PreventEventEffectHandler : EffectHandlerBase<PreventEventEffectParams>
    {
        public PreventEventEffectHandler(ILogger<PreventEventEffectHandler> logger)
            : base(logger)
        {
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, PreventEventEffectParams parameters)
        {
            return new GameStateActionResult
            {
                Success = true,
                EventIntercept = EventInterceptResult.PreventEvent()
            };
        }
    }
}
