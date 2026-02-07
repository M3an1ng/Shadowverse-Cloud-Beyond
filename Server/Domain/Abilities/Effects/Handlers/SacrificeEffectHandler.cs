using System;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.Sacrifice)]
    public class SacrificeEffectHandler : EffectHandlerBase<SacrificeEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public SacrificeEffectHandler(
            ILogger<SacrificeEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, SacrificeEffectParams parameters)
        {
            const string effectName = "SacrificeEffect";

            if (parameters.Amount <= 0)
            {
                return new GameStateActionResult { Success = true };
            }

            Func<TargetInfo, GameContext, DomainOperationResult> playerAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != TargetType.Player || targetInfo.Player == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a player or player data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Sacrifice effect can only be applied to players." };
                    }

                    var player = targetInfo.Player;
                    var oldValue = player.BloodPool;
                    if (!player.TryConsumeCounter(PlayerState.BloodPoolCounterKey, parameters.Amount))
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "Not enough blood pool to consume." };
                    }

                    var newValue = player.BloodPool;
                    var result = new DomainOperationResult { Success = true };
                    result.GeneratedEvents.Add(new CounterChangedEvent(player, PlayerState.BloodPoolCounterKey, oldValue, newValue));
                    return result;
                };

            var playerTargets = PlayerScopeResolver.ResolvePlayerTargets(
                context,
                parameters.PlayerScope);

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                playerTargets,
                playerAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for sacrifice effect, or no valid targets found.";
            }

            return serviceResult;
        }
    }
}
