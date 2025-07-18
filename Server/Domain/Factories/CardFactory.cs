// File: Server/Domain/Factories/CardFactory.cs
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Dtos.Enums;
using System;
using ShadowverseBeyond.Server.Services.Abstractions; // For IGameDataProvider

namespace ShadowverseBeyond.Server.Domain.Factories
{
    public class CardFactory : ICardFactory
    {
        private readonly IGameDataProvider _gameDataProvider;
        private readonly ILogger<CardFactory> _logger;

        public CardFactory(IGameDataProvider gameDataProvider, ILogger<CardFactory> logger)
        {
            _gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public CardInstance CreateAndRegisterNewCard(GameState gameState, int cardDefinitionId, Guid ownerPlayerId, CardZone initialZone)
        {
            if (gameState == null)
            {
                _logger.LogError("GameState cannot be null in CreateAndRegisterNewCard.");
                throw new ArgumentNullException(nameof(gameState));
            }

            var cardDefinition = _gameDataProvider.GetCardDefinition(cardDefinitionId);
            if (cardDefinition == null)
            {
                _logger.LogError("CardDefinition not found for ID: {CardDefinitionId}", cardDefinitionId);
                // Consider throwing a specific exception or returning null/default
                throw new ArgumentException($"CardDefinition not found for ID: {cardDefinitionId}", nameof(cardDefinitionId));
            }

            Guid instanceId = Guid.NewGuid();
            var cardInstance = new CardInstance(instanceId, cardDefinition.Id, ownerPlayerId);

            cardInstance.InitializeFromDefinition(cardDefinition);
            cardInstance.CurrentZone = initialZone;
            // TODO: Set FieldPosition if initialZone is Field

            gameState.RegisterCardInstance(cardInstance);
            _logger.LogInformation("Created and registered new card instance {InstanceId} (Def: {DefinitionId}) for player {OwnerPlayerId} in zone {InitialZone}",
                instanceId, cardDefinitionId, ownerPlayerId, initialZone);

            return cardInstance;
        }

        public CardInstance CreateAndRegisterCardCopy(GameState gameState, CardInstance originalCardToCopy, Guid newOwnerPlayerId, CardZone initialZone, bool exactCopy)
        {
            if (gameState == null)
            {
                _logger.LogError("GameState cannot be null in CreateAndRegisterCardCopy.");
                throw new ArgumentNullException(nameof(gameState));
            }
            if (originalCardToCopy == null)
            {
                _logger.LogError("OriginalCardToCopy cannot be null in CreateAndRegisterCardCopy.");
                throw new ArgumentNullException(nameof(originalCardToCopy));
            }

            var cardDefinition = _gameDataProvider.GetCardDefinition(originalCardToCopy.DefinitionId);
            if (cardDefinition == null)
            {
                _logger.LogError("CardDefinition not found for original card's DefinitionId: {DefinitionId}", originalCardToCopy.DefinitionId);
                throw new ArgumentException($"CardDefinition not found for original card's DefinitionId: {originalCardToCopy.DefinitionId}", nameof(originalCardToCopy.DefinitionId));
            }

            Guid newInstanceId = Guid.NewGuid();
            var newCardCopy = new CardInstance(newInstanceId, originalCardToCopy.DefinitionId, newOwnerPlayerId);

            newCardCopy.InitializeFromDefinition(cardDefinition);

            if (exactCopy)
            {
                // Basic state copy
                newCardCopy.CurrentCost = originalCardToCopy.CurrentCost;
                newCardCopy.CurrentAttack = originalCardToCopy.CurrentAttack;
                newCardCopy.MaxHealth = originalCardToCopy.MaxHealth; // Set MaxHealth first
                newCardCopy.CurrentHealth = originalCardToCopy.CurrentHealth; // Then CurrentHealth
                newCardCopy.IsEvolved = originalCardToCopy.IsEvolved;
                newCardCopy.AttacksThisTurn = originalCardToCopy.AttacksThisTurn;
                newCardCopy.CanAttack = originalCardToCopy.CanAttack; // Copy summon sickness or readiness

                // Copy keywords (base and temporary)
                newCardCopy.SetBaseKeywords(originalCardToCopy.BaseKeywords);
                foreach (var keyword in originalCardToCopy.TemporaryKeywords)
                {
                    newCardCopy.AddKeyword(keyword);
                }

                // TODO: Deep copy buffs - this can be complex and might need IBuffManager or similar
                // For now, ActiveEffects and AbilityState are not copied.
                _logger.LogWarning("Exact copy for buffs and complex ability states is not fully implemented yet for card {OriginalInstanceId}", originalCardToCopy.InstanceId);
            }
            // If not exactCopy, it's a fresh instance based on definition, which is already done.

            newCardCopy.CurrentZone = initialZone;
            // TODO: Set FieldPosition if initialZone is Field

            gameState.RegisterCardInstance(newCardCopy);
            _logger.LogInformation("Created and registered a copy ({NewInstanceId}, Def: {DefinitionId}) of card {OriginalInstanceId} for player {NewOwnerPlayerId} in zone {InitialZone}. ExactCopy: {ExactCopy}",
                newInstanceId, newCardCopy.DefinitionId, originalCardToCopy.InstanceId, newOwnerPlayerId, initialZone, exactCopy);

            return newCardCopy;
        }
    }
}