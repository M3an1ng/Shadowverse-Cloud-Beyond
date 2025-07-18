using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Interfaces; // Added for ICardRepository
using ShadowverseBeyond.Server.Exceptions; // Added for GameLogicException
using ShadowverseBeyond.Server.Domain.Abilities; // For AbilityManager and AbilityConfig
using ShadowverseBeyond.Server.Services.Abstractions; // For IGameDataProvider
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the effect of transforming a card instance into another card definition.
    /// </summary>
    public class TransformEffectHandler : EffectHandlerBase<TransformEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        private readonly AbilityManager _abilityManager;
        private readonly IGameDataProvider _gameDataProvider;

        public TransformEffectHandler(
            ILogger<TransformEffectHandler> logger,
            ITargetProcessingService targetProcessingService,
            AbilityManager abilityManager,
            IGameDataProvider gameDataProvider)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
            _abilityManager = abilityManager ?? throw new ArgumentNullException(nameof(abilityManager));
            _gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, TransformEffectParams parameters)
        {
            const string effectName = "TransformEffect";
            Logger.LogDebug("{EffectName}: Starting with TargetCardDefinitionId={TargetDefId}.",
                           effectName, parameters.TargetCardDefinitionId);

            var targetDefinition = _gameDataProvider.GetCardDefinition(parameters.TargetCardDefinitionId);
            if (targetDefinition == null)
            {
                Logger.LogError("{EffectName}: Target CardDefinition with ID {TargetDefId} not found.",
                               effectName, parameters.TargetCardDefinitionId);
                return new GameStateActionResult { Success = false, ErrorMessage = $"Target CardDefinition ID {parameters.TargetCardDefinitionId} not found." };
            }

            Func<CardInstance, GameContext, Task<DomainOperationResult>> cardTransformAction =
                (card, ctx) =>
                {
                    // 1. Perform the basic transformation on the card instance (stats, definitionId, etc.)
                    var transformOperationResult = card.Transform(targetDefinition);
                    if (!transformOperationResult.Success)
                    {
                        // If basic transform fails, bubble up the error.
                        return Task.FromResult(transformOperationResult);
                    }

                    // 2. Unregister all old abilities associated with this card instance.
                    _abilityManager.UnregisterCardAbilities(card.InstanceId);
                    Logger.LogDebug("{EffectName}: Unregistered old abilities for card {CardId} (InstanceId: {InstanceId}) after transform.",
                                   effectName, targetDefinition.Name, card.InstanceId);

                    // 3. Get ability configurations for the new card definition.
                    var newAbilityConfigs = _gameDataProvider.GetAbilityConfigs(targetDefinition.Id);
                    if (newAbilityConfigs != null && newAbilityConfigs.Any())
                    {
                        // 4. Create and register new abilities for the transformed card.
                        _abilityManager.CreateAbilitiesForCard(card, newAbilityConfigs);
                        Logger.LogDebug("{EffectName}: Created and registered {Count} new abilities for transformed card {CardName} (InstanceId: {InstanceId}).",
                                       effectName, newAbilityConfigs.Count, targetDefinition.Name, card.InstanceId);
                    }
                    else
                    {
                        Logger.LogDebug("{EffectName}: Transformed card {CardName} (InstanceId: {InstanceId}) has no new abilities to register.",
                                       effectName, targetDefinition.Name, card.InstanceId);
                    }

                    // The original transformOperationResult already contains the CardTransformedEvent.
                    // We are augmenting the process here by handling ability updates.
                    // The overall success depends on the initial transform.
                    return Task.FromResult(transformOperationResult);
                };

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                null,
                cardTransformAction,
                effectName);
        }
    }
}