using System;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.RewriteEventParams)]
    public class RewriteEventParamsEffectHandler : EffectHandlerBase<RewriteEventParamsEffectParams>
    {
        public RewriteEventParamsEffectHandler(ILogger<RewriteEventParamsEffectHandler> logger)
            : base(logger)
        {
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, RewriteEventParamsEffectParams parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters.ParamName))
            {
                return new GameStateActionResult
                {
                    Success = false,
                    ErrorMessage = "RewriteEventParamsEffect requires a non-empty ParamName."
                };
            }

            var result = new GameStateActionResult { Success = true };
            result.EventIntercept = EventInterceptResult.RewriteParameter(parameters.ParamName, parameters.Value);
            return result;
        }
    }
}
