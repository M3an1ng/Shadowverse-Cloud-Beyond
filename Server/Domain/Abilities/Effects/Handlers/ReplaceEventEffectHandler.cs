using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.ReplaceEvent)]
    public class ReplaceEventEffectHandler : EffectHandlerBase<ReplaceEventEffectParams>
    {
        public ReplaceEventEffectHandler(ILogger<ReplaceEventEffectHandler> logger)
            : base(logger)
        {
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, ReplaceEventEffectParams parameters)
        {
            var result = new GameStateActionResult { Success = true };
            result.EventIntercept = EventInterceptResult.ReplaceEvent(
                parameters.ReplacementEventType,
                parameters.ReplacementParams);
            return result;
        }
    }
}
