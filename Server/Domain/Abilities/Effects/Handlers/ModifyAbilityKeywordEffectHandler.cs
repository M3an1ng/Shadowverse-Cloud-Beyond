using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.ModifyAbilityKeyword)]
    public class ModifyAbilityKeywordEffectHandler : EffectHandlerBase<ModifyAbilityKeywordEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public ModifyAbilityKeywordEffectHandler(
            ILogger<ModifyAbilityKeywordEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, ModifyAbilityKeywordEffectParams parameters)
        {
            const string effectName = "ModifyAbilityKeywordEffect";

            var modification = BuildModification(parameters);
            if (modification == null)
            {
                return new GameStateActionResult { Success = true };
            }

            var effectKey = BuildEffectKey(effect);

            Func<TargetInfo, GameContext, DomainOperationResult> modifyAbilityKeywordAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != TargetType.Card || targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "ModifyAbilityKeyword effect can only be applied to cards." };
                    }

                    var target = targetInfo.Card;
                    var buff = CreateBuff(ctx, target, modification, parameters, effect.sourceAbilityId, effectKey);
                    return ctx.BuffService.ApplyBuffProcess(buff, ctx, parameters.CanStack);
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                modifyAbilityKeywordAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for modify ability keyword effect, or no valid targets found.";
            }

            return serviceResult;
        }

        private static AbilityKeywordModification? BuildModification(ModifyAbilityKeywordEffectParams parameters)
        {
            if (parameters.Keyword == AbilityKeyword.None || !ShouldApply(parameters.ValueModifier, parameters.ModifierLayer))
            {
                return null;
            }

            return new AbilityKeywordModification(
                parameters.Keyword,
                NormalizeModifierValue(parameters.ValueModifier!.Value, parameters.ModifierLayer),
                parameters.ModifierLayer);
        }

        private static Buff CreateBuff(
            GameContext context,
            CardInstance target,
            AbilityKeywordModification modification,
            ModifyAbilityKeywordEffectParams parameters,
            Guid? effectSourceAbilityId,
            string? effectKey)
        {
            var sourceAbility = context.GetCurrentAbility();
            var sourceAbilityId = sourceAbility?.RuntimeId ?? effectSourceAbilityId ?? Guid.Empty;
            var ownerPlayerId = context.SourceCard?.OwnerPlayerId ?? target.OwnerPlayerId;
            var ownerPlayer = context.CurrentGameState.GetPlayerState(ownerPlayerId)
                ?? context.CurrentGameState.GetPlayerState(context.RequestingPlayerId);
            var currentTurn = ownerPlayer?.TurnNumber ?? 0;

            var buff = new Buff(
                sourceAbilityId: sourceAbilityId,
                targetCardInstanceId: target.InstanceId,
                currentTurn: currentTurn,
                effects: new List<IBuffEffect>
                {
                    new AbilityKeywordBuffEffect(new List<AbilityKeywordModification> { modification })
                },
                durationType: parameters.DurationType,
                durationParameter: parameters.DurationType == DurationType.ConditionalRemoval ? null : parameters.DurationParameter,
                removalCondition: parameters.RemoveCondition);

            if (!string.IsNullOrWhiteSpace(effectKey) && sourceAbilityId != Guid.Empty)
            {
                buff.EffectKey = effectKey;
            }

            return buff;
        }

        private static bool ShouldApply(int? value, ModifierLayer modifierLayer)
        {
            if (!value.HasValue)
            {
                return false;
            }

            if (modifierLayer == ModifierLayer.Set)
            {
                return true;
            }

            if (modifierLayer == ModifierLayer.Multiplicative)
            {
                return value.Value != 100;
            }

            return value.Value != 0;
        }

        private static double NormalizeModifierValue(int rawValue, ModifierLayer layer)
        {
            return layer == ModifierLayer.Multiplicative
                ? rawValue / 100d
                : rawValue;
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
