using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.TriggerAbility)]
    public class TriggerAbilityEffectHandler : EffectHandlerBase<TriggerAbilityEffectParams>
    {
        public TriggerAbilityEffectHandler(ILogger<TriggerAbilityEffectHandler> logger)
            : base(logger)
        {
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, TriggerAbilityEffectParams parameters)
        {
            // Runtime ability invocation is orchestrated by GameActionTransactionService
            // after EffectCompletedEvent has been materialized.
            return new GameStateActionResult
            {
                Success = true,
                ReturnValue = parameters
            };
        }
    }
}
