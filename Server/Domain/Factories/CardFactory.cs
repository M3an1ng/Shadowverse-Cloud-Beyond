// File: Server/Domain/Factories/CardFactory.cs
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Dtos.Enums;
using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Identifiers;

using ShadowverseBeyond.Server.Domain.Abilities;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
namespace ShadowverseBeyond.Server.Domain.Factories
{
    public class CardFactory : ICardFactory
    {
        private readonly ILogger<CardFactory> _logger;
        private readonly IBuffFactory _buffFactory;
        private readonly IBuffManager _buffManager;

        public CardFactory(ILogger<CardFactory> logger, IBuffFactory buffFactory, IBuffManager buffManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _buffFactory = buffFactory ?? throw new ArgumentNullException(nameof(buffFactory));
            _buffManager = buffManager ?? throw new ArgumentNullException(nameof(buffManager));
        }

        public CardInstance CreateAndRegisterNewCard(GameState gameState, int cardDefinitionId, Guid ownerPlayerId, CardZone initialZone)
        {
            if (gameState == null)
            {
                _logger.LogError("GameState cannot be null in CreateAndRegisterNewCard.");
                throw new ArgumentNullException(nameof(gameState));
            }

            var cardDefinition = GameDataProvider.Instance.GetCardDefinition(cardDefinitionId);
            if (cardDefinition == null)
            {
                _logger.LogError("Failed to create card. CardDefinition not found for ID: {CardDefinitionId}", cardDefinitionId);
                // Consider throwing a specific exception or returning null/default
                throw new ArgumentException($"CardDefinition not found for ID: {cardDefinitionId}", nameof(cardDefinitionId));
            }

            Guid instanceId = Guid.NewGuid();
            var cardInstance = new CardInstance(instanceId, cardDefinition.Id, ownerPlayerId);

            cardInstance.CurrentZone = initialZone;
            cardInstance.ZoneIndex = null;

            gameState.RegisterCardInstance(cardInstance);
            // var readableCardId = gameState.InGameIds.Format(InGameIdKind.Card, instanceId);
            // _logger.LogInformation("Created and registered new card instance {CardId} (guid={InstanceId}, Def: {DefinitionId}) for player {OwnerPlayerId} in zone {InitialZone}",
            //     readableCardId, instanceId, cardDefinitionId, ownerPlayerId, initialZone);

            var abilityConfigs = GameDataProvider.Instance.GetAbilityConfigs(cardDefinition.Id);
            if (abilityConfigs != null && abilityConfigs.Any())
            {
                if (gameState.AbilityManager == null)
                {
                    _logger.LogWarning("AbilityManager is null when creating card {InstanceId}; skipping ability registration.", instanceId);
                }
                else
                {
                    var abilities = gameState.AbilityManager.CreateAbilitiesForCard(cardInstance, abilityConfigs, null, gameState);
                    foreach (var ability in abilities)
                    {
                        gameState.InGameIds.GetOrCreate(InGameIdKind.Ability, ability.RuntimeId);
                    }
                }
            }

            // 【关键修复】将卡牌实例添加到其所属玩家的对应区域列表中
            var ownerPlayer = gameState.GetPlayerState(ownerPlayerId);
            if (ownerPlayer != null)
            {
                // 【BUG修复】直接获取区域列表的引用，而不是通过一个返回副本的方法。
                // 这是导致牌库为空的根本原因。
                switch (initialZone)
                {
                    case CardZone.Deck:
                        ownerPlayer.Deck.Add(cardInstance);
                        break;
                    case CardZone.Hand:
                        ownerPlayer.Hand.Add(cardInstance);
                        break;
                    case CardZone.Field:
                        if (cardDefinition.Type != CardType.Leader)
                        {
                            ownerPlayer.Field.Add(cardInstance);
                        }
                        break;
                    case CardZone.Graveyard:
                        ownerPlayer.Graveyard.Add(cardInstance);
                        break;
                    case CardZone.Extra:
                        ownerPlayer.Extra.Add(cardInstance);
                        break;
                    case CardZone.Emblem:
                        ownerPlayer.Emblem.Add(cardInstance);
                        break;
                    default:
                        _logger.LogWarning("Attempted to add card {InstanceId} to an unhandled zone: {InitialZone}", instanceId, initialZone);
                        break;
                }

                InitializeZoneMetadataOnAppend(ownerPlayer, cardInstance, initialZone);
                // _logger.LogInformation("Added card instance {CardId} (guid={InstanceId}) to Player {OwnerPlayerId}'s {InitialZone} list.",
                //     readableCardId, instanceId, ownerPlayerId, initialZone);
            }
            else
            {
                _logger.LogWarning("Could not find owner player state for ID {OwnerPlayerId} to add card to zone list.", ownerPlayerId);
            }

            return cardInstance;
        }

        public CardInstance CreateAndRegisterCardCopy(GameState gameState, CardInstance originalCardToCopy, Guid newOwnerPlayerId, CardZone initialZone, bool exactCopy, CardInstance? copyInstance = null)
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

            CardInstance newCardCopy;
            Guid newInstanceId;
            if (copyInstance != null)
            {
                if (copyInstance.CurrentDefinitionId != originalCardToCopy.CurrentDefinitionId)
                {
                    _logger.LogError("Copy instance definition mismatch. Expected {DefinitionId}, got {CopyDefinitionId}.",
                        originalCardToCopy.CurrentDefinitionId, copyInstance.CurrentDefinitionId);
                    throw new ArgumentException("Copy instance definition mismatch.", nameof(copyInstance));
                }

                newCardCopy = copyInstance;
                if (newCardCopy.InstanceId == Guid.Empty)
                {
                    newCardCopy.InstanceId = Guid.NewGuid();
                }
                newCardCopy.OwnerPlayerId = newOwnerPlayerId;
                newInstanceId = newCardCopy.InstanceId;
            }
            else
            {
                // The definition is implicitly handled by the CardInstance constructor and its CurrentDefinitionId
                newInstanceId = Guid.NewGuid();
                newCardCopy = new CardInstance(newInstanceId, originalCardToCopy.CurrentDefinitionId, newOwnerPlayerId);
            }

            if (exactCopy)
            {
                newCardCopy.CopyRuntimeStateFrom(originalCardToCopy);
                newCardCopy.ResetAttackState();
                newCardCopy.ResetActState();
                newCardCopy.ResetFusionState();

                if (gameState.FindCardInstance(newCardCopy.InstanceId) == null)
                {
                    gameState.RegisterCardInstance(newCardCopy);
                }

                var runtimeBuffManager = gameState.BuffManager ?? _buffManager;

                // Use the BuffFactory to create deep copies of each buff.
                // Use the BuffFactory to create deep copies of each buff and register them to the new card.
                var originalBuffs = runtimeBuffManager.GetBuffsForCard(originalCardToCopy.InstanceId) ?? new List<Buff>();
                foreach (var buff in originalBuffs)
                {
                    // Use the enhanced factory method to create a copy already retargeted to the new card.
                    var buffCopy = _buffFactory.CreateBuffCopy(buff, newCardCopy);
                    // Use the low-level, atomic registration method. This is the correct layer for a factory.
                    // TODO: This path is currently dead code. If it's ever used,
                    // it needs a GameContext to correctly register buffs with abilities.
                    // For now, we pass the available GameState to satisfy the method signature.
                    runtimeBuffManager.RegisterBuffInstance(buffCopy, gameState);
                }

                // When making an exact copy, the new card should also get a fresh set of abilities based on its definition.
                // When making an exact copy, the new card should also get a fresh set of abilities based on its definition.
                var abilityConfigs = GameDataProvider.Instance.GetAbilityConfigs(originalCardToCopy.CurrentDefinitionId);
                if (abilityConfigs != null && abilityConfigs.Any())
                {
                    if (gameState.AbilityManager == null)
                    {
                        _logger.LogWarning("AbilityManager is null when creating copied card {InstanceId}; skipping ability registration.", newInstanceId);
                    }
                    else
                    {
                        var abilities = gameState.AbilityManager.CreateAbilitiesForCard(newCardCopy, abilityConfigs, null, gameState);
                        foreach (var ability in abilities)
                        {
                            gameState.InGameIds.GetOrCreate(InGameIdKind.Ability, ability.RuntimeId);
                        }
                    }
                }

                var copiedBuffCount = runtimeBuffManager.GetBuffsForCard(newCardCopy.InstanceId)?.Count ?? 0;
                _logger.LogInformation("Created an exact copy of card {OriginalInstanceId} as {NewInstanceId}. Copied {BuffCount} buffs and {Damage} damage.",
                   originalCardToCopy.InstanceId, newInstanceId, copiedBuffCount, newCardCopy.Damage);
            }
            // If not exactCopy, it's a fresh instance based on its definition, which is the default state.

            newCardCopy.CurrentZone = initialZone;
            newCardCopy.ZoneIndex = null;

            if (gameState.FindCardInstance(newCardCopy.InstanceId) == null)
            {
                gameState.RegisterCardInstance(newCardCopy);
            }
            var ownerPlayer = gameState.GetPlayerState(newOwnerPlayerId);
            if (ownerPlayer != null)
            {
                switch (initialZone)
                {
                    case CardZone.Deck:
                        ownerPlayer.Deck.Add(newCardCopy);
                        break;
                    case CardZone.Hand:
                        ownerPlayer.Hand.Add(newCardCopy);
                        break;
                    case CardZone.Field:
                        if (!newCardCopy.IsLeader)
                        {
                            ownerPlayer.Field.Add(newCardCopy);
                        }
                        break;
                    case CardZone.Graveyard:
                        ownerPlayer.Graveyard.Add(newCardCopy);
                        break;
                    case CardZone.Extra:
                        ownerPlayer.Extra.Add(newCardCopy);
                        break;
                    case CardZone.Emblem:
                        ownerPlayer.Emblem.Add(newCardCopy);
                        break;
                    default:
                        _logger.LogWarning("Attempted to add copied card {InstanceId} to an unhandled zone: {InitialZone}", newInstanceId, initialZone);
                        break;
                }

                InitializeZoneMetadataOnAppend(ownerPlayer, newCardCopy, initialZone);
            }
            else
            {
                _logger.LogWarning("Could not find owner player state for ID {OwnerPlayerId} to add copied card to zone list.", newOwnerPlayerId);
            }
            // var readableNewId = gameState.InGameIds.Format(InGameIdKind.Card, newInstanceId);
            // var readableOriginalId = gameState.InGameIds.Format(InGameIdKind.Card, originalCardToCopy.InstanceId);
            // _logger.LogInformation("Created and registered a copy {NewCardId} (guid={NewInstanceId}, Def: {DefinitionId}) of card {OriginalCardId} (guid={OriginalInstanceId}) for player {NewOwnerPlayerId} in zone {InitialZone}. ExactCopy: {ExactCopy}",
            //     readableNewId, newInstanceId, newCardCopy.CurrentDefinitionId, readableOriginalId, originalCardToCopy.InstanceId, newOwnerPlayerId, initialZone, exactCopy);

            return newCardCopy;
        }

        private static void InitializeZoneMetadataOnAppend(PlayerState ownerPlayer, CardInstance card, CardZone zone)
        {
            var zoneList = ResolveZoneList(ownerPlayer, zone);
            if (zoneList == null)
            {
                card.ZoneIndex = null;
                return;
            }

            var index = zoneList.LastIndexOf(card);
            if (index < 0)
            {
                card.ZoneIndex = null;
                return;
            }

            card.ZoneIndex = index;
        }

        private static List<CardInstance>? ResolveZoneList(PlayerState ownerPlayer, CardZone zone)
        {
            return zone switch
            {
                CardZone.Deck => ownerPlayer.Deck,
                CardZone.Hand => ownerPlayer.Hand,
                CardZone.Field => ownerPlayer.Field,
                CardZone.Graveyard => ownerPlayer.Graveyard,
                CardZone.Extra => ownerPlayer.Extra,
                CardZone.Emblem => ownerPlayer.Emblem,
                _ => null
            };
        }
    }
}
