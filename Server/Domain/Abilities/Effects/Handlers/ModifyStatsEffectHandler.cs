using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Dtos.Enums; // Added for CardZone

using ShadowverseBeyond.Server.Services.Abstractions; // Added for IBuffService
namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    using ShadowverseBeyond.Server.Domain.Buffs;
    using ShadowverseBeyond.Server.Domain.Abilities.Common;

    public class ModifyStatsEffectHandler : EffectHandlerBase<ModifyStatsEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        private readonly IBuffFactory _buffFactory;
        private readonly IBuffManager _buffManager;
        private readonly IBuffService _buffService;

        public ModifyStatsEffectHandler(ILogger<ModifyStatsEffectHandler> logger,
                                        ITargetProcessingService targetProcessingService,
                                        IBuffFactory buffFactory,
                                        IBuffManager buffManager,
                                        IBuffService buffService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
            _buffFactory = buffFactory ?? throw new ArgumentNullException(nameof(buffFactory));
            _buffManager = buffManager ?? throw new ArgumentNullException(nameof(buffManager));
            _buffService = buffService ?? throw new ArgumentNullException(nameof(buffService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, ModifyStatsEffectParams parameters)
        {
            const string effectName = "ModifyStatsEffect";
            var sourceAbility = context.CurrentAbility;

            if (sourceAbility?.SourceCard == null)
            {
                Logger.LogError("{EffectName}: Source ability or its source card not found in context.", effectName);
                return new GameStateActionResult { Success = false, ErrorMessage = "Source ability or card is missing." };
            }

            var ownerPlayer = context.CurrentGameState.GetPlayerState(sourceAbility.SourceCard.OwnerPlayerId);
            if (ownerPlayer == null)
            {
                Logger.LogError("{EffectName}: Owner player not found for source card {CardId}.", effectName, sourceAbility.SourceCard.InstanceId);
                return new GameStateActionResult { Success = false, ErrorMessage = "Owner player not found." };
            }

            Func<CardInstance, GameContext, Task<DomainOperationResult>> cardModifyAction =
                async (card, ctx) =>
                {
                    var domainResult = new DomainOperationResult { Success = true };
                    var modifications = new List<StatModification>();

                    // Get stats before applying any buff
                    int oldEffectiveAttack = await _buffService.GetEffectiveAttackAsync(card, ctx);
                    int oldEffectiveMaxHealth = await _buffService.GetEffectiveMaxHealthAsync(card, ctx);
                    int oldEffectiveCost = await _buffService.GetEffectiveCostAsync(card, ctx);

                    if (parameters.AttackModifier.HasValue && parameters.AttackModifier.Value != 0)
                        modifications.Add(new StatModification(StatType.Attack, parameters.AttackModifier.Value, ModifierLayer.Additive));

                    if (parameters.HealthModifier.HasValue && parameters.HealthModifier.Value != 0)
                    {
                        // HealthModifier affects MaxHealth (and CurrentHealth adjusts via CardInstance logic)
                        modifications.Add(new StatModification(StatType.Defense, parameters.HealthModifier.Value, ModifierLayer.Additive));
                    }

                    if (parameters.CostModifier.HasValue && parameters.CostModifier.Value != 0)
                        modifications.Add(new StatModification(StatType.Cost, parameters.CostModifier.Value, ModifierLayer.Additive));

                    if (!modifications.Any())
                    {
                        Logger.LogInformation("{EffectName}: No stat modifiers provided or all modifiers are zero.", effectName);
                        return domainResult;
                    }

                    var buff = _buffFactory.CreateStatBuff(
                        target: card,
                        sourceCard: sourceAbility.SourceCard,
                        sourceAbility: sourceAbility,
                        statModifications: modifications,
                        durationType: DurationType.Permanent,
                        durationParameter: null,
                        currentTurn: ownerPlayer.TurnNumber,
                        isNonStackable: false
                    );

                    await _buffManager.RegisterBuffAsync(buff);
                    domainResult.GeneratedEvents.Add(new BuffAppliedEvent(buff, sourceAbility.SourceCard));

                    // Get stats after applying the buff
                    int newEffectiveAttack = await _buffService.GetEffectiveAttackAsync(card, ctx);
                    int newEffectiveMaxHealth = await _buffService.GetEffectiveMaxHealthAsync(card, ctx);
                    // CurrentHealth is handled internally by CardInstance when MaxHealth changes.
                    // ModifyStatsEffect does not directly report CurrentHealth changes beyond what MaxHealth implies.
                    int newEffectiveCost = await _buffService.GetEffectiveCostAsync(card, ctx);

                    if (oldEffectiveAttack != newEffectiveAttack ||
                        oldEffectiveMaxHealth != newEffectiveMaxHealth ||
                        oldEffectiveCost != newEffectiveCost)
                    {
                        // StatChangedEvent now takes newMaxHealth as its health parameter
                        domainResult.GeneratedEvents.Add(new StatChangedEvent(
                            ownerPlayer,
                            card.InstanceId,
                            newEffectiveAttack,
                            newEffectiveMaxHealth, // Pass the new MaxHealth
                            newEffectiveCost
                        ));
                    }

                    // No separate HealthChangedEvent here as per user feedback for ModifyStatsEffect.
                    // CurrentHealth changes are a consequence of MaxHealth changes within CardInstance.

                    return domainResult;
                };

            var serviceResult = await _targetProcessingService.ProcessTargetsAsync(
                context,
                null,
                cardModifyAction,
                effectName);
            // Removed duplicate effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for modify stats effect, or no valid targets found.";
            }

            // The EffectCompletedEvent will be created by the EffectExecutor.
            // This handler is now only responsible for invoking the domain logic
            // and returning the atomic events generated by that logic.
            return serviceResult;
        }

        // CreateEffectCompletedEvent method is removed as this responsibility has moved to EffectExecutor.
    }
}