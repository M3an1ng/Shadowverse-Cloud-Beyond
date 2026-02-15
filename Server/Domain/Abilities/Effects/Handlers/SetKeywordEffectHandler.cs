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
    /// <summary>
    /// Handles the SetKeyword effect, generating events to add or remove a keyword from target cards.
    /// </summary>
    [EffectHandler(EffectType.SetKeyword)]
    public class SetKeywordEffectHandler : EffectHandlerBase<SetKeywordEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public SetKeywordEffectHandler(
            ILogger<SetKeywordEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, SetKeywordEffectParams parameters)
        {
            const string effectName = "SetKeywordEffect";
            Logger.LogDebug("{EffectName}: Starting with Operation={Operation}, Keyword={Keyword}.", effectName, parameters.Operation, parameters.Keyword);

            if (parameters.Keyword == CardKeyword.None)
            {
                Logger.LogDebug("{EffectName}: Keyword is None. No operation performed.", effectName);
                return new GameStateActionResult { Success = true };
            }

            Func<TargetInfo, GameContext, DomainOperationResult> action = (targetInfo, _) =>
            {
                if (targetInfo.Type != TargetType.Card || targetInfo.Card == null)
                {
                    Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                    return new DomainOperationResult { Success = false, ErrorMessage = "SetKeyword effect can only be applied to cards." };
                }

                var result = new DomainOperationResult { Success = true };
                var effectKey = BuildEffectKey(effect);
                switch (parameters.Operation)
                {
                    case KeywordOperation.Add:
                        result.GeneratedEvents.Add(new KeywordAddedEvent(
                            parameters.Keyword,
                            targetInfo.Card,
                            context.SourceCard,
                            parameters.DurationType,
                            parameters.DurationParameter,
                            effectKey,
                            parameters.CanStack));
                        break;
                    case KeywordOperation.Remove:
                        result.GeneratedEvents.Add(new KeywordRemovedEvent(
                            parameters.Keyword,
                            targetInfo.Card,
                            context.SourceCard,
                            parameters.DurationType,
                            parameters.DurationParameter,
                            effectKey,
                            parameters.CanStack));
                        break;
                    default:
                        return new DomainOperationResult { Success = false, ErrorMessage = $"Unsupported keyword operation: {parameters.Operation}" };
                }

                return result;
            };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                action,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for SetKeyword effect, or no valid targets found.";
            }

            return serviceResult;
        }

        private static string? BuildEffectKey(Effect effect)
        {
            if (!effect.sourceAbilityId.HasValue)
            {
                return null;
            }

            return $"{effect.sourceAbilityId.Value:N}:{effect.EffectIndex}";
        }
    }
}
