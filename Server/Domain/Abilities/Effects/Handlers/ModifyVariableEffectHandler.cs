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
    [EffectHandler(EffectType.ModifyVariable)]
    public class ModifyVariableEffectHandler : EffectHandlerBase<ModifyCounterEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public ModifyVariableEffectHandler(
            ILogger<ModifyVariableEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, ModifyCounterEffectParams parameters)
        {
            const string effectName = "ModifyVariableEffect";
            if (string.IsNullOrWhiteSpace(parameters.CounterName))
            {
                return new GameStateActionResult { Success = false, ErrorMessage = "CounterName is required." };
            }

            IReadOnlyList<TargetInfo> targets;
            if (parameters.TargetType == TargetType.Card)
            {
                targets = effect.SelectedTargets?.Where(t => t.Type == TargetType.Card).ToList()
                    ?? new List<TargetInfo>();
                if (targets.Count == 0)
                {
                    return new GameStateActionResult { Success = false, ErrorMessage = "ModifyVariable requires card targets when TargetType is Card." };
                }
            }
            else
            {
                targets = PlayerScopeResolver.ResolvePlayerTargets(context, parameters.PlayerScope);
            }

            Func<TargetInfo, GameContext, DomainOperationResult> action =
                (targetInfo, ctx) =>
                {
                    if (parameters.TargetType == TargetType.Player && targetInfo.Player != null)
                    {
                        return ApplyToPlayer(targetInfo.Player, parameters);
                    }

                    if (parameters.TargetType == TargetType.Card && targetInfo.Card != null)
                    {
                        return ApplyToCard(targetInfo.Card, parameters);
                    }

                    return new DomainOperationResult { Success = false, ErrorMessage = "ModifyVariable effect requires player or card targets." };
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                targets,
                action,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for modify variable effect, or no valid targets found.";
            }

            return serviceResult;
        }

        private static DomainOperationResult ApplyToPlayer(PlayerState player, ModifyCounterEffectParams parameters)
        {
            var key = parameters.CounterName;
            var oldValue = player.GetCounter(key);
            var newValue = oldValue;

            switch (parameters.Operation)
            {
                case CounterOperation.Add:
                    newValue = player.AddCounter(key, parameters.Amount);
                    break;
                case CounterOperation.Set:
                    newValue = player.SetCounter(key, parameters.Amount);
                    break;
                case CounterOperation.Consume:
                    if (parameters.Amount <= 0)
                    {
                        return new DomainOperationResult { Success = true };
                    }
                    if (!player.TryConsumeCounter(key, parameters.Amount))
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "Not enough counter value to consume." };
                    }
                    newValue = player.GetCounter(key);
                    break;
                default:
                    return new DomainOperationResult { Success = false, ErrorMessage = "Unsupported counter operation." };
            }

            var result = new DomainOperationResult { Success = true };
            if (newValue != oldValue)
            {
                result.GeneratedEvents.Add(new CounterChangedEvent(player, key, oldValue, newValue));
                if (string.Equals(key, PlayerState.GraveyardCounterKey, StringComparison.OrdinalIgnoreCase))
                {
                    result.GeneratedEvents.Add(new GraveyardUpdatedEvent(player, newValue));
                }
            }

            return result;
        }

        private static DomainOperationResult ApplyToCard(CardInstance card, ModifyCounterEffectParams parameters)
        {
            var key = parameters.CounterName;
            var oldValue = card.GetCounter(key);
            var newValue = oldValue;

            switch (parameters.Operation)
            {
                case CounterOperation.Add:
                    newValue = card.AddCounter(key, parameters.Amount);
                    break;
                case CounterOperation.Set:
                    newValue = card.SetCounter(key, parameters.Amount);
                    break;
                case CounterOperation.Consume:
                    if (parameters.Amount <= 0)
                    {
                        return new DomainOperationResult { Success = true };
                    }
                    if (!card.TryConsumeCounter(key, parameters.Amount))
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "Not enough counter value to consume." };
                    }
                    newValue = card.GetCounter(key);
                    break;
                default:
                    return new DomainOperationResult { Success = false, ErrorMessage = "Unsupported counter operation." };
            }

            var result = new DomainOperationResult { Success = true };
            if (newValue != oldValue)
            {
                result.GeneratedEvents.Add(new CounterChangedEvent(card, key, oldValue, newValue));
            }

            return result;
        }
    }
}
