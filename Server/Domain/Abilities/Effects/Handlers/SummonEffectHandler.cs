using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For TargetId, TargetIdType
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters; // For SummonUnitEffectParams
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services; // For ITargetProcessingService
using ShadowverseBeyond.Server.Domain.Interfaces; // For ICardRepository
using ShadowverseBeyond.Server.Domain.Constants; // For GameConstants, CardKeyword
using ShadowverseBeyond.Server.Services.Abstractions; // For IGameDataProvider (assuming location)
using ShadowverseBeyond.Server.Dtos; // For CardZone
using ShadowverseBeyond.Server.Domain.Abilities.Runtime; // For RuntimeAbility
// Removed using for DTO: using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the application of the SummonUnit effect.
    /// Targets Players to summon units onto their field.
    /// </summary>
    public class SummonEffectHandler : EffectHandlerBase<SummonUnitEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        private readonly ICardRepository _cardRepository;
        private readonly IGameDataProvider _gameDataProvider; // To create new CardInstances

        public SummonEffectHandler(
            ILogger logger,
            ITargetProcessingService targetProcessingService,
            ICardRepository cardRepository,
            IGameDataProvider gameDataProvider) // Inject dependencies
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
            _cardRepository = cardRepository ?? throw new ArgumentNullException(nameof(cardRepository));
            _gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
        }

        /// <summary>
        /// Applies the SummonUnit effect to the selected player targets.
        /// </summary>
        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, SummonUnitEffectParams parameters)
        {
            const string effectName = "SummonUnitEffect";

            // Validate CardDefinitionId
            if (parameters.CardDefinitionId <= 0)
            {
                Logger.LogError("{EffectName}: Invalid CardDefinitionId ({CardDefId}) provided.", effectName, parameters.CardDefinitionId);
                return new GameStateActionResult { Success = false, ErrorMessage = "Invalid CardDefinitionId for SummonUnit effect." };
            }

            // Fetch Card Definition (consider caching if performance becomes an issue)
            var cardDefinition = await _cardRepository.GetCardDefinitionByIdAsync(parameters.CardDefinitionId);
            if (cardDefinition == null)
            {
                Logger.LogError("{EffectName}: CardDefinition with ID {CardDefId} not found.", effectName, parameters.CardDefinitionId);
                return new GameStateActionResult { Success = false, ErrorMessage = $"CardDefinition {parameters.CardDefinitionId} not found." };
            }

            // Validate if the card type is summonable (Follower or Amulet typically)
            // Assuming CardDefinition has a Type property (string or enum)
            if (cardDefinition.Type != "Follower" && cardDefinition.Type != "Amulet") // Adjust based on actual CardDefinition structure
            {
                Logger.LogWarning("{EffectName}: Attempting to summon non-summonable card type '{CardType}' (ID: {CardDefId}).",
                                  effectName, cardDefinition.Type, parameters.CardDefinitionId);
                // Decide if this is an error or just skip. Let's treat as error for now.
                return new GameStateActionResult { Success = false, ErrorMessage = $"Cannot summon card type '{cardDefinition.Type}'." };
            }


            // Define the action for summoning a unit for a PlayerState
            Func<PlayerState, GameContext, Task<IndividualActionResult>> playerSummonAction =
                async (player, ctx) =>
                {
                    var generatedEvents = new List<IGameEvent>();
                    int summonedCount = 0;
                    bool fieldWasFull = false;
                    var summonedInstanceIds = new List<Guid>(); // Track IDs of successfully summoned units

                    Logger.LogDebug("{EffectName}: Attempting to summon {SummonCount} instances of CardDefId {CardDefId} for Player {PlayerId}",
                                     effectName, parameters.SummonCount, parameters.CardDefinitionId, player.PlayerId);

                    for (int i = 0; i < parameters.SummonCount; i++)
                    {
                        // Check field capacity using the correct path
                        if (player.Field.Count >= GameConstants.Field.MaxFieldSize)
                        {
                            Logger.LogWarning("{EffectName}: Player {PlayerId}'s field is full ({FieldCount}/{MaxField}). Cannot summon more units.",
                                               effectName, player.PlayerId, player.Field.Count, GameConstants.Field.MaxFieldSize);
                            fieldWasFull = true;
                            break; // Stop summoning if field is full
                        }

                        // Get the Card Definition from the provider
                        var definitionToSummon = _gameDataProvider.GetCardDefinition(parameters.CardDefinitionId);
                        if (definitionToSummon == null)
                        {
                            // This should have been caught earlier, but double-check
                            Logger.LogError("{EffectName}: Could not find CardDefinition {CardDefId} during instance creation for Player {PlayerId}.",
                                             effectName, parameters.CardDefinitionId, player.PlayerId);
                            continue; // Skip this summon attempt
                        }

                        // Use the public constructor
                        var newInstance = new CardInstance(definitionToSummon.Id, player.PlayerId)
                        {
                            // InstanceId is set by constructor
                            // DefinitionId is set by constructor
                            // OwnerPlayerId is set by constructor
                            CurrentZone = parameters.TargetZone, // Usually Field
                            // Use null-coalescing for nullable definition stats
                            BaseAttack = definitionToSummon.Attack ?? 0,
                            BaseHealth = definitionToSummon.Health ?? 0,
                            CurrentAttack = definitionToSummon.Attack ?? 0,
                            // CurrentHealth and MaxHealth are set based on BaseHealth by constructor/setter logic
                            MaxHealth = definitionToSummon.Health ?? 0, // Set MaxHealth first
                            CurrentHealth = definitionToSummon.Health ?? 0, // Then CurrentHealth
                            BaseCost = definitionToSummon.Cost, // Set BaseCost (Cost is int, remove ??)
                            CurrentCost = definitionToSummon.Cost, // Set CurrentCost (Cost is int, remove ??)
                            IsEvolved = false,
                            CanAttack = false, // Summoning sickness (unless Rush/Storm)
                            AttacksThisTurn = 0,
                            // Keywords are handled below using SetBaseKeywords
                            // Abilities cannot be set directly
                            // Effects list doesn't exist this way
                            CardType = definitionToSummon.Type ?? string.Empty, // Set CardType
                            Name = definitionToSummon.Name ?? string.Empty, // Set Name
                            Description = definitionToSummon.Description ?? string.Empty, // Set Description
                            Rarity = definitionToSummon.Rarity ?? string.Empty, // Set Rarity
                            Class = definitionToSummon.Class ?? string.Empty, // Set Class
                            Tribe = definitionToSummon.Tribe // Set Tribe
                        };

                        // Set Base Keywords after instance creation
                        newInstance.SetBaseKeywords(definitionToSummon.Keywords ?? Enumerable.Empty<string>());

                        // Adjust CanAttack based on keywords (Storm/Rush)
                        if (newInstance.HasKeyword(CardKeyword.Storm) || newInstance.HasKeyword(CardKeyword.Rush))
                        {
                            newInstance.CanAttack = true;
                        }


                        // TODO: Add logic here to create RuntimeAbility instances from definitionToSummon.Abilities
                        // This might involve calling AbilityManager or a similar service.
                        // For now, Abilities list remains empty.

                        // Set initial state (zone, etc.) - Mostly done during initialization

                        // 使用PlayerState领域方法将单位添加到场上
                        var fieldEvents = player.AddCardToField(newInstance);
                        generatedEvents.AddRange(fieldEvents);
                        summonedCount++;
                        summonedInstanceIds.Add(newInstance.InstanceId);

                        Logger.LogTrace("{EffectName}: Summoned CardInstance {InstanceId} (DefId: {CardDefId}) to Player {PlayerId}'s field. Field count: {FieldCount}",
                                         effectName, newInstance.InstanceId, newInstance.DefinitionId, player.PlayerId, player.Field.Count);

                        // Generate SummonEvent (or similar domain event)
                        // Assuming SummonEvent exists and takes PlayerStateDto and CardInstance
                        // Need IMapper here or handle mapping later. For now, create event with IDs.
                        // generatedEvents.Add(new SummonEvent(player.PlayerId, newInstance.InstanceId)); // Simplified event for now
                        // Use the correct domain event: CardSummonedEvent
                        generatedEvents.Add(new CardSummonedEvent(
                            sourcePlayerId: ctx.RequestingPlayerId, // Player causing the summon
                            summonedCard: newInstance,
                            targetZone: newInstance.CurrentZone
                        ));


                        // TODO: Trigger "Fanfare" or "OnSummon" abilities if applicable
                        // This might involve adding another event or calling AbilityManager
                    }

                    string message = $"Summoned {summonedCount}/{parameters.SummonCount} units (DefId: {parameters.CardDefinitionId}).";
                    if (fieldWasFull) message += " Field became full.";
                    if (summonedCount == 0 && parameters.SummonCount > 0) message = "Field was full, no units summoned.";

                    return new IndividualActionResult
                    {
                        TargetId = TargetId.Player(player.PlayerId),
                        Success = true, // Considered successful even if field was full, as the attempt was made
                        Message = message,
                        GeneratedEvents = generatedEvents // Include events for each summon
                    };
                };

            // Call the target processing service, expecting Player targets
            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                TargetIdType.Player,
                playerSummonAction,
                null, // No Card action
                effectName);
        }
    }
}