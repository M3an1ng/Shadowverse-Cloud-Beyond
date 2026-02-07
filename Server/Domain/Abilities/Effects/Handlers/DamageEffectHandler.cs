using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using System.Linq; // Added for OfType, FirstOrDefault, Any, Select
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue; // Added for DestroyEffectRequest
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the application of the Damage effect.
    /// </summary>
    // Inherit from EffectHandlerBase instead of IEffectHandler directly
    [EffectHandler(EffectType.Damage)]
    public class DamageEffectHandler : EffectHandlerBase<DamageEffectParams>
    {
        // Remove private _logger, use base.Logger or Logger directly
        private readonly ITargetProcessingService _targetProcessingService; // Inject the service

        // Adjust constructor signature to accept ILogger and call base(logger) correctly
        public DamageEffectHandler(ILogger<DamageEffectHandler> logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, DamageEffectParams parameters)
        {
            const string effectName = "DamageEffect";
            Logger.LogDebug("{EffectName}: Starting with DamageAmount={Amount}.", effectName, parameters.DamageAmount);

            if (parameters.DamageAmount <= 0)
            {
                Logger.LogInformation("{EffectName}: DamageAmount is {DamageAmount}. Emitting zero-damage events.", effectName, parameters.DamageAmount);

                Func<TargetInfo, GameContext, DomainOperationResult> zeroDamageAction =
                    (targetInfo, ctx) =>
                    {
                        if (targetInfo.Type != Common.TargetType.Card || targetInfo.Card == null)
                        {
                            Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                            return new DomainOperationResult { Success = false, ErrorMessage = "Damage effect can only be applied to cards." };
                        }

                        var result = new DomainOperationResult { Success = true };
                        var sourceCard = ctx.SourceCard ?? targetInfo.Card;
                        result.GeneratedEvents.Add(new DamageEvent(targetInfo.Card, 0, sourceCard, DamageKind.Ability));
                        return result;
                    };

                var zeroResult = _targetProcessingService.ProcessTargets(
                    context,
                    effect.SelectedTargets,
                    zeroDamageAction,
                    effectName);

                if (!zeroResult.Success && string.IsNullOrEmpty(zeroResult.ErrorMessage))
                {
                    zeroResult.ErrorMessage = "Failed to process targets for zero damage effect, or no valid targets found.";
                }

                return zeroResult;
            }

            if (parameters.DistributionMode == DamageDistributionMode.Split)
            {
                return ApplySplitDamage(effect, context, parameters, effectName);
            }

            Func<TargetInfo, GameContext, DomainOperationResult> damageAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Card || targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Damage effect can only be applied to cards." };
                    }

                    var card = targetInfo.Card;
                    return card.TakeDamage(parameters.DamageAmount, ctx.SourceCard);
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                damageAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for damage effect, or no valid targets found.";
            }

            return serviceResult;
        }

        // CreateEffectCompletedEvent method is removed as this responsibility has moved to EffectExecutor.

        private GameStateActionResult ApplySplitDamage(Effect effect, GameContext context, DamageEffectParams parameters, string effectName)
        {
            var result = new GameStateActionResult();
            var targets = effect.SelectedTargets ?? new List<TargetInfo>();

            if (targets.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "Split damage requires at least one target.";
                return result;
            }

            var cards = targets
                .Where(t => t.Type == Common.TargetType.Card && t.Card != null)
                .Select(t => t.Card!)
                .GroupBy(c => c.InstanceId)
                .Select(g => g.First())
                .ToList();

            if (cards.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "Split damage can only be applied to card targets.";
                return result;
            }

            var leaderTarget = cards.FirstOrDefault(c => c.IsLeader);
            var followerTargets = cards
                .Where(c => !c.IsLeader)
                .OrderBy(c => c.ZoneIndex ?? int.MaxValue)
                .ThenBy(c => c.InstanceId)
                .ToList();

            var remaining = parameters.DamageAmount;

            if (followerTargets.Count == 0)
            {
                if (leaderTarget == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Split damage could not resolve a valid target.";
                    return result;
                }

                var leaderResult = leaderTarget.TakeDamage(remaining, context.SourceCard);
                result.GeneratedEvents.AddRange(leaderResult.GeneratedEvents);
                result.Results.Add(leaderResult);
                result.Success = leaderResult.Success;
                return result;
            }

            for (var i = 0; i < followerTargets.Count && remaining > 0; i++)
            {
                var target = followerTargets[i];
                var isLastFollower = i == followerTargets.Count - 1;
                var maxAssignable = Math.Max(0, target.CurrentHealth);
                var assignedDamage = isLastFollower && leaderTarget == null
                    ? remaining
                    : Math.Min(remaining, maxAssignable);

                if (assignedDamage <= 0)
                {
                    continue;
                }

                var op = target.TakeDamage(assignedDamage, context.SourceCard);
                result.GeneratedEvents.AddRange(op.GeneratedEvents);
                result.Results.Add(op);
                remaining -= assignedDamage;
            }

            if (leaderTarget != null && remaining > 0)
            {
                var leaderResult = leaderTarget.TakeDamage(remaining, context.SourceCard);
                result.GeneratedEvents.AddRange(leaderResult.GeneratedEvents);
                result.Results.Add(leaderResult);
                remaining = 0;
            }

            result.Success = result.GeneratedEvents.Count > 0;
            if (!result.Success && string.IsNullOrEmpty(result.ErrorMessage))
            {
                result.ErrorMessage = "Split damage did not produce any events.";
            }

            return result;
        }
    }
}
