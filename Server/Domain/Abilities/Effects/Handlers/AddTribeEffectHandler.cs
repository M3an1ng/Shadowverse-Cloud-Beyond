using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.AddTribe)]
    public class AddTribeEffectHandler : EffectHandlerBase<AddTribeEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        private readonly IBuffService _buffService;

        public AddTribeEffectHandler(
            ILogger<AddTribeEffectHandler> logger,
            ITargetProcessingService targetProcessingService,
            IBuffService buffService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
            _buffService = buffService ?? throw new ArgumentNullException(nameof(buffService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, AddTribeEffectParams parameters)
        {
            const string effectName = "AddTribeEffect";
            var tribes = ParseTribes(parameters.Tribe);
            if (tribes.Count == 0)
            {
                return new GameStateActionResult { Success = true };
            }

            var effectKey = BuildEffectKey(effect);
            Func<TargetInfo, GameContext, DomainOperationResult> action = (targetInfo, ctx) =>
            {
                if (targetInfo.Type != TargetType.Card || targetInfo.Card == null)
                {
                    Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                    return new DomainOperationResult { Success = false, ErrorMessage = "AddTribe effect can only be applied to cards." };
                }

                return _buffService.ApplyTribeBuff(
                    ctx,
                    targetInfo.Card,
                    tribes,
                    parameters.DurationType,
                    parameters.DurationParameter,
                    effectKey,
                    parameters.CanStack);
            };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                action,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for AddTribe effect, or no valid targets found.";
            }

            return serviceResult;
        }

        private static List<string> ParseTribes(string? raw)
        {
            return (raw ?? string.Empty)
                .Split([',', '，'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(static tribe => !string.IsNullOrWhiteSpace(tribe))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
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
