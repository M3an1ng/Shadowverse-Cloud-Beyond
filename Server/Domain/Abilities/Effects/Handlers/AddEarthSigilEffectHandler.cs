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
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Factories;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.AddEarthSigil)]
    public class AddEarthSigilEffectHandler : EffectHandlerBase<AddEarthSigilEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        private readonly ICardFactory _cardFactory;

        public AddEarthSigilEffectHandler(
            ILogger<AddEarthSigilEffectHandler> logger,
            ITargetProcessingService targetProcessingService,
            ICardFactory cardFactory)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
            _cardFactory = cardFactory ?? throw new ArgumentNullException(nameof(cardFactory));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, AddEarthSigilEffectParams parameters)
        {
            const string effectName = "AddEarthSigilEffect";

            if (parameters.Amount <= 0)
            {
                return new GameStateActionResult { Success = false, ErrorMessage = "AddEarthSigil amount must be positive." };
            }

            Func<TargetInfo, GameContext, DomainOperationResult> playerAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != TargetType.Player || targetInfo.Player == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a player or player data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "AddEarthSigil effect can only be applied to players." };
                    }

                    var player = targetInfo.Player;
                    var earthSigil = player.Field.FirstOrDefault(card => card.HasKeyword(CardKeyword.EarthSigil));

                    if (earthSigil != null)
                    {
                        var oldValue = earthSigil.GetCounter(CardInstance.EarthsigilCounterKey);
                        var newValue = earthSigil.AddCounter(CardInstance.EarthsigilCounterKey, parameters.Amount);

                        var result = new DomainOperationResult { Success = true };
                        if (newValue != oldValue)
                        {
                            result.GeneratedEvents.Add(new CounterChangedEvent(earthSigil, CardInstance.EarthsigilCounterKey, oldValue, newValue));
                        }

                        return result;
                    }

                    if (parameters.TokenDefinitionId <= 0)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "AddEarthSigil requires TokenDefinitionId when no EarthSigil is present." };
                    }

                    var token = _cardFactory.CreateAndRegisterNewCard(
                        ctx.CurrentGameState,
                        parameters.TokenDefinitionId,
                        player.PlayerId,
                        CardZone.Staging);

                    token.SetCounter(CardInstance.EarthsigilCounterKey, parameters.Amount);

                    var summonResult = new DomainOperationResult { Success = true };
                    var expectedPosition = player.Field.Count + 1;
                    summonResult.GeneratedEvents.Add(new SummonEvent(player, token, expectedPosition, EventReasonSource.Ability));
                    return summonResult;
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
                serviceResult.ErrorMessage = "Failed to process targets for add earth sigil effect, or no valid targets found.";
            }

            return serviceResult;
        }
    }
}
