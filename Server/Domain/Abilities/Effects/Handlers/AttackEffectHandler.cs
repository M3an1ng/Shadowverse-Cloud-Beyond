using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.Attack)]
    public class AttackEffectHandler : EffectHandlerBase<AttackEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public AttackEffectHandler(ILogger<AttackEffectHandler> logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService;
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, AttackEffectParams parameters)
        {
            const string effectName = "AttackEffect";
            var attacker = context.SourceCard;

            if (attacker == null)
            {
                Logger.LogError("{EffectName}: Attacker not found in GameContext.SourceCard.", effectName);
                return new GameStateActionResult { Success = false, ErrorMessage = "Attacker not found in context." };
            }

            var attackerPlayer = context.CurrentGameState.GetPlayerState(attacker.OwnerPlayerId);
            if (attackerPlayer == null)
            {
                Logger.LogError("{EffectName}: Attacker's owner (ID: {OwnerId}) not found.", effectName, attacker.OwnerPlayerId);
                return new GameStateActionResult { Success = false, ErrorMessage = "Attacker's owner not found." };
            }


            // Phase 2: Command Creation. For each valid target, create a committable event.
            Func<TargetInfo, GameContext, DomainOperationResult> createAttackEventAction =
                (targetInfo, ctx) =>
                {
                    var result = new DomainOperationResult();
                    if (targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target card data is missing for one of the targets.", effectName);
                        result.Success = false;
                        result.ErrorMessage = "Attack target is not a valid card.";
                        return result;
                    }

                    var targetUnit = targetInfo.Card;

                    // Phase 1: Gatekeeping. Perform all precondition checks before creating any events.
                    if (!attacker.CanPerformAttack(context.CurrentGameState, attackerPlayer, targetUnit, parameters.ForceAttack))
                    {
                        Logger.LogWarning("{EffectName}: Attack preconditions not met for Attacker (ID: {AttackerId}).", effectName, attacker.InstanceId);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Attack preconditions not met." };
                    }

                    // The handler's job is to create the event (the intent).
                    // The transaction service will later call its Commit() method.
                    var attackEvent = new AttackEvent(attacker, targetUnit);
                    result.GeneratedEvents.Add(attackEvent);
                    result.GeneratedEvents.Add(new CombatResolutionEvent(attacker, targetUnit));
                    result.Success = true;

                    return result;
                };

            // Phase 3: Dispatching. Use the service to process all selected targets.
            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                createAttackEventAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for attack effect, or no valid targets found.";
            }

            return serviceResult;
        }
    }
}
