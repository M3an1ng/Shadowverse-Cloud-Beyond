using System;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Buffs
{
    public interface IBuffEvaluator
    {
        bool IsApplicable(Buff buff, CardInstance target, GameContext context);
    }

    /// <summary>
    /// Evaluates buff applicability without mutating state.
    /// Duration-based expiry is handled by lifecycle refresh, not here.
    /// </summary>
    public class BuffEvaluator : IBuffEvaluator
    {
        private readonly IConditionEvaluator _conditionEvaluator;
        private readonly ILogger<BuffEvaluator> _logger;

        public BuffEvaluator(IConditionEvaluator conditionEvaluator, ILogger<BuffEvaluator> logger)
        {
            _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsApplicable(Buff buff, CardInstance target, GameContext context)
        {
            if (!buff.IsActive)
            {
                return false;
            }

            if (buff.ApplicabilityCondition == null)
            {
                return true;
            }

            try
            {
                return _conditionEvaluator.Evaluate(buff.ApplicabilityCondition, context);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to evaluate ApplicabilityCondition for Buff {BuffId} on Card {CardId}.",
                    buff.Id, target.InstanceId);
                return false;
            }
        }
    }
}
