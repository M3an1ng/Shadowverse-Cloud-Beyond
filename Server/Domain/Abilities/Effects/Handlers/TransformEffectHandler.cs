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

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the effect of transforming a card instance into another card definition.
    /// </summary>
    public class TransformEffectHandler : EffectHandlerBase<TransformEffectParams>
    {
        private readonly ICardRepository _cardRepository;
        private readonly ITargetProcessingService _targetProcessingService;

        public TransformEffectHandler(ILogger logger, ICardRepository cardRepository, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _cardRepository = cardRepository ?? throw new ArgumentNullException(nameof(cardRepository));
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, TransformEffectParams parameters)
        {
            const string effectName = "TransformEffect";
            Logger.LogDebug("{EffectName}: Starting with TargetCardDefinitionId={TargetDefId}.",
                           effectName, parameters.TargetCardDefinitionId);

            // Validate target card definition first
            var targetDefinition = await _cardRepository.GetCardDefinitionByIdAsync(parameters.TargetCardDefinitionId);
            if (targetDefinition == null)
            {
                Logger.LogError("{EffectName}: Target CardDefinition with ID {TargetDefId} not found.",
                               effectName, parameters.TargetCardDefinitionId);
                return new GameStateActionResult { Success = false, ErrorMessage = $"Target CardDefinition ID {parameters.TargetCardDefinitionId} not found." };
            }

            // Define the action for transforming a CardInstance
            Func<CardInstance, GameContext, Task<IndividualActionResult>> cardTransformAction =
                async (card, ctx) =>
                {
                    Logger.LogDebug("{EffectName}: Transforming card {InstanceId} (DefId: {OldDefId}) into DefId: {NewDefId}.",
                                   effectName, card.InstanceId, card.DefinitionId, targetDefinition.Id);

                    // Update the existing instance with the new definition's properties
                    // Keep the same InstanceId and OwnerPlayerId
                    card.DefinitionId = targetDefinition.Id;
                    card.BaseAttack = targetDefinition.Attack ?? 0;
                    card.BaseHealth = targetDefinition.Health ?? 0;
                    card.CurrentAttack = targetDefinition.Attack ?? 0; // Reset current stats to new base
                    card.CurrentHealth = targetDefinition.Health ?? 0;
                    card.MaxHealth = targetDefinition.Health ?? 0;
                    card.CardType = targetDefinition.Type; // Update card type
                    card.BaseCost = targetDefinition.Cost;
                    card.CurrentCost = targetDefinition.Cost; // Reset cost
                    card.IsEvolved = false; // Transformed units are typically not evolved initially
                    card.Countdown = targetDefinition.Type.Equals("Amulet", StringComparison.OrdinalIgnoreCase) ? targetDefinition.Health : null;

                    // Update keywords and abilities
                    card.SetBaseKeywords(targetDefinition.Keywords?.Select(k => Enum.TryParse<CardKeyword>(k, true, out var kw) ? kw : CardKeyword.None).Where(k => k != CardKeyword.None) ?? Enumerable.Empty<CardKeyword>());
                    card.ClearTemporaryKeywords(); // Clear keywords from previous form
                    // TODO: Integrate with AbilityManager to correctly update/replace abilities on transform.

                    // TODO: Generate TransformEvent when event is defined
                    // var transformEvent = new TransformEvent(card.InstanceId, targetDefinition.Id);
                    var individualEvents = new List<IGameEvent>(); // { transformEvent };

                    Logger.LogInformation("{EffectName}: Transformed card {InstanceId} into DefId {NewDefId}.",
                                         effectName, card.InstanceId, targetDefinition.Id);

                    await Task.CompletedTask; // Transformation is synchronous

                    return new IndividualActionResult
                    {
                        TargetId = TargetId.Card(card.InstanceId),
                        Success = true,
                        Message = $"Transformed card into DefId {targetDefinition.Id}.",
                        GeneratedEvents = individualEvents
                    };
                };

            // Transform effect only targets CardInstances
            Logger.LogDebug("{EffectName}: Processing targets assuming CardInstance type.", effectName);

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                TargetIdType.CardInstance, // Expect only CardInstance targets
                null, // No PlayerState action needed
                cardTransformAction,
                effectName);
        }
    }
}