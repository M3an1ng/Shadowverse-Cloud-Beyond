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
    [EffectHandler(EffectType.Necromancy)]
    public class NecromancyEffectHandler : EffectHandlerBase<NecromancyEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public NecromancyEffectHandler(
            ILogger<NecromancyEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, NecromancyEffectParams parameters)
        {
            const string effectName = "NecromancyEffect";

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
                        return new DomainOperationResult { Success = false, ErrorMessage = "Necromancy effect can only be applied to players." };
                    }

                    var player = targetInfo.Player;
                    if (player.GraveyardCount < parameters.Amount)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "Not enough graveyard count to consume." };
                    }

                    player.GraveyardCount = Math.Max(0, player.GraveyardCount - parameters.Amount);
                    var result = new DomainOperationResult { Success = true };
                    result.GeneratedEvents.Add(new GraveyardUpdatedEvent(player, player.GraveyardCount));
                    return result;
                };

            var playerTargets = PlayerScopeResolver.ResolvePlayerTargets(
                context.CurrentGameState,
                context.RequestingPlayerId,
                parameters.PlayerScope);

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                playerTargets,
                playerAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for necromancy effect, or no valid targets found.";
            }

            return serviceResult;
        }
    }
}
