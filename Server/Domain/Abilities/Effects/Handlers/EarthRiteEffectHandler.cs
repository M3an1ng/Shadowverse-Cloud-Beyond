using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.EarthRite)]
    public class EarthRiteEffectHandler : EffectHandlerBase<EarthRiteEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public EarthRiteEffectHandler(
            ILogger<EarthRiteEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, EarthRiteEffectParams parameters)
        {
            const string effectName = "EarthRiteEffect";

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
                        return new DomainOperationResult { Success = false, ErrorMessage = "EarthRite effect can only be applied to players." };
                    }

                    var player = targetInfo.Player;
                    var earthSigil = player.Field.FirstOrDefault(card => card.HasKeyword(CardKeyword.EarthSigil));
                    if (earthSigil == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "No EarthSigil present to consume." };
                    }

                    var oldValue = earthSigil.GetCounter(CardInstance.EarthsigilCounterKey);
                    if (oldValue < parameters.Amount)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "Not enough EarthSigil to consume." };
                    }

                    var newValue = earthSigil.AddCounter(CardInstance.EarthsigilCounterKey, -parameters.Amount);
                    var result = new DomainOperationResult { Success = true };
                    if (newValue != oldValue)
                    {
                        result.GeneratedEvents.Add(new CounterChangedEvent(earthSigil, CardInstance.EarthsigilCounterKey, oldValue, newValue));
                    }

                    if (newValue == 0)
                    {
                        result.GeneratedEvents.Add(new DestroyEvent(earthSigil, earthSigil, EventReasonSource.System));
                    }
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
                serviceResult.ErrorMessage = "Failed to process targets for earth rite effect, or no valid targets found.";
            }

            return serviceResult;
        }
    }
}
