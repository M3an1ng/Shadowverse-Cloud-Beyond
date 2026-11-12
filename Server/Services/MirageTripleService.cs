using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    public sealed record MirageTripleCandidateGroup(
        string TripleGroupId,
        int BaseDefinitionId,
        IReadOnlyList<CardInstance> MaterialCards);

    public interface IMirageTripleService
    {
        string ResolveTripleGroupId(int cardDefinitionId);

        MirageTripleCandidateGroup? FindNextTripleCandidate(PlayerState player, string? requiredTripleGroupId = null);

        IReadOnlyList<MirageTripleSynthesizedEvent> CreatePendingTripleEvents(PlayerState player, int? roundNumber = null);

        DomainOperationResult ExecuteTripleSynthesis(
            PlayerState player,
            string tripleGroupId,
            IReadOnlyList<Guid>? requestedMaterialCardIds,
            GameContext context,
            out CardInstance? synthesizedCard,
            out MirageDiscoverTicket? grantedTicket);

        MirageDiscoverTicket? CreateDiscoverTicket(MirageMatchState? mirage, int targetTier, MirageRuleConfig rules, bool allowAboveMaxTier = false);
    }

    public sealed class MirageTripleService : IMirageTripleService
    {
        private readonly IMirageShopService _shopService;
        private readonly IGameDataProvider _gameDataProvider;

        public MirageTripleService(
            IMirageShopService shopService,
            IGameDataProvider gameDataProvider)
        {
            _shopService = shopService ?? throw new ArgumentNullException(nameof(shopService));
            _gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
        }

        public string ResolveTripleGroupId(int cardDefinitionId)
        {
            if (cardDefinitionId <= 0)
            {
                return string.Empty;
            }

            return _shopService.GetCatalogEntry(cardDefinitionId)?.TripleGroupId
                   ?? cardDefinitionId.ToString();
        }

        public MirageTripleCandidateGroup? FindNextTripleCandidate(PlayerState player, string? requiredTripleGroupId = null)
        {
            if (player == null)
            {
                return null;
            }

            var eligibleCards = player.Field
                .Concat(player.Hand)
                .Where(c => c != null
                    && c.IsFollower
                    && !c.IsLeader
                    && !c.IsMirageTriple
                    && c.BaseDefinitionId > 0
                    && (c.CurrentZone == CardZone.Field || c.CurrentZone == CardZone.Hand))
                .ToList();

            if (eligibleCards.Count < 3)
            {
                return null;
            }

            var currentHandCount = player.Hand.Count;
            foreach (var group in eligibleCards.GroupBy(c => ResolveTripleGroupId(c.BaseDefinitionId), StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(group.Key))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(requiredTripleGroupId)
                    && !string.Equals(group.Key, requiredTripleGroupId, StringComparison.Ordinal))
                {
                    continue;
                }

                var materials = SelectTripleMaterialsWithCapacity(group.ToList(), currentHandCount);
                if (materials != null && materials.Count == 3)
                {
                    return new MirageTripleCandidateGroup(
                        group.Key,
                        materials[0].BaseDefinitionId,
                        materials);
                }
            }

            return null;
        }

        private static List<CardInstance>? SelectTripleMaterialsWithCapacity(
            IReadOnlyList<CardInstance> groupCards,
            int currentHandCount)
        {
            if (groupCards == null || groupCards.Count < 3)
            {
                return null;
            }

            // Ensure that after retiring the 3 selected material cards, Hand has room for the new Triple card:
            // currentHandCount - handMaterialsSelected < MaxHandSize
            // => handMaterialsSelected >= currentHandCount - MaxHandSize + 1
            var requiredHandMaterials = Math.Max(0, currentHandCount - GameConstants.Hand.MaxHandSize + 1);
            if (requiredHandMaterials == 0)
            {
                return groupCards.Take(3).ToList();
            }

            if (requiredHandMaterials > 3)
            {
                return null;
            }

            var handMaterials = groupCards.Where(c => c.CurrentZone == CardZone.Hand).ToList();
            if (handMaterials.Count < requiredHandMaterials)
            {
                return null;
            }

            var selected = new List<CardInstance>(3);
            var selectedIds = new HashSet<Guid>();

            foreach (var handCard in handMaterials.Take(requiredHandMaterials))
            {
                selected.Add(handCard);
                selectedIds.Add(handCard.InstanceId);
            }

            foreach (var card in groupCards)
            {
                if (selected.Count >= 3)
                {
                    break;
                }

                if (selectedIds.Add(card.InstanceId))
                {
                    selected.Add(card);
                }
            }

            return selected.Count == 3 ? selected : null;
        }

        public IReadOnlyList<MirageTripleSynthesizedEvent> CreatePendingTripleEvents(PlayerState player, int? roundNumber = null)
        {
            var candidate = FindNextTripleCandidate(player);
            if (candidate == null)
            {
                return Array.Empty<MirageTripleSynthesizedEvent>();
            }

            return new[]
            {
                new MirageTripleSynthesizedEvent(
                    player.PlayerId,
                    candidate.TripleGroupId,
                    candidate.MaterialCards.Select(c => c.InstanceId).ToList(),
                    expectedRoundNumber: roundNumber)
            };
        }

        public DomainOperationResult ExecuteTripleSynthesis(
            PlayerState player,
            string tripleGroupId,
            IReadOnlyList<Guid>? requestedMaterialCardIds,
            GameContext context,
            out CardInstance? synthesizedCard,
            out MirageDiscoverTicket? grantedTicket)
        {
            synthesizedCard = null;
            grantedTicket = null;

            if (player == null) throw new ArgumentNullException(nameof(player));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var gameState = context.CurrentGameState;
            if (gameState.Mode != GameMode.Mirage || gameState.Mirage == null || player.Mirage == null)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "Triple synthesis is only valid in Mirage mode."
                };
            }

            var cardFactory = context.ServiceProvider.GetService<ICardFactory>();
            if (cardFactory == null)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "ICardFactory is unavailable for triple synthesis."
                };
            }

            var allEligible = player.Field
                .Concat(player.Hand)
                .Where(c => c != null
                    && c.IsFollower
                    && !c.IsLeader
                    && !c.IsMirageTriple
                    && c.BaseDefinitionId > 0
                    && (c.CurrentZone == CardZone.Field || c.CurrentZone == CardZone.Hand))
                .ToList();

            List<CardInstance>? materials = null;

            if (requestedMaterialCardIds != null && requestedMaterialCardIds.Count == 3)
            {
                var distinctIds = requestedMaterialCardIds.Distinct().ToList();
                if (distinctIds.Count != 3)
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = "Triple synthesis requires 3 distinct material card instance IDs."
                    };
                }

                var byId = allEligible.ToDictionary(c => c.InstanceId);
                if (distinctIds.All(id => byId.ContainsKey(id)))
                {
                    var candidateMaterials = distinctIds.Select(id => byId[id]).ToList();
                    var groupIds = candidateMaterials.Select(c => ResolveTripleGroupId(c.BaseDefinitionId)).Distinct(StringComparer.Ordinal).ToList();
                    if (groupIds.Count != 1 || string.IsNullOrWhiteSpace(groupIds[0]))
                    {
                        return new DomainOperationResult
                        {
                            Success = false,
                            ErrorMessage = "All 3 material cards must share the same TripleGroupId."
                        };
                    }

                    var requestedHandCount = candidateMaterials.Count(c => c.CurrentZone == CardZone.Hand);
                    if (player.Hand.Count - requestedHandCount < GameConstants.Hand.MaxHandSize)
                    {
                        materials = candidateMaterials;
                    }
                    else
                    {
                        // Fallback to capacity-aware selection within the same group if the requested 3 were all on Field while Hand is full
                        var fallback = FindNextTripleCandidate(player, groupIds[0]);
                        if (fallback != null)
                        {
                            materials = fallback.MaterialCards.ToList();
                        }
                    }
                }
            }

            if (materials == null)
            {
                var candidate = FindNextTripleCandidate(player, string.IsNullOrWhiteSpace(tripleGroupId) ? null : tripleGroupId);
                if (candidate == null)
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = $"No eligible 3-card triple group '{tripleGroupId}' with hand capacity found for player {player.PlayerId}."
                    };
                }

                materials = candidate.MaterialCards.ToList();
            }

            var handMaterialCount = materials.Count(c => c.CurrentZone == CardZone.Hand);
            if (player.Hand.Count - handMaterialCount >= GameConstants.Hand.MaxHandSize)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "Hand does not have capacity for the synthesized triple card."
                };
            }

            // Contract 3.2 Step 1: Extract non-aura Permanent buffs from the 3 material cards before retiring them.
            // AbilityBuffEffects are deduplicated by AbilityConfig signature so granted abilities are only applied once.
            var seenGrantedAbilitySignatures = new HashSet<string>(StringComparer.Ordinal);
            var extractedBuffNodes = new List<JsonObject>();

            foreach (var material in materials)
            {
                var materialBuffs = context.BuffManager.GetBuffsForCard(material.InstanceId);
                foreach (var buff in materialBuffs)
                {
                    if (buff == null
                        || !buff.IsActive
                        || buff.DurationType != DurationType.Permanent
                        || MirageCombatSnapshotBuilder.IsAuraGeneratedBuff(buff, context.AbilityManager))
                    {
                        continue;
                    }

                    var rawElement = JsonSerializer.SerializeToElement(buff, ReplayExecutionState.Json);
                    var clonedBuff = ReplayExecutionState.RestoreBuff(rawElement);
                    if (clonedBuff.Effects == null || clonedBuff.Effects.Count == 0)
                    {
                        continue;
                    }

                    var filteredEffects = new List<IBuffEffect>();
                    foreach (var effect in clonedBuff.Effects)
                    {
                        if (effect == null)
                        {
                            continue;
                        }

                        if (effect is AbilityBuffEffect abilityBuffEffect)
                        {
                            var dedupedConfigs = (abilityBuffEffect.GrantedAbilityConfigs ?? new List<AbilityConfig>())
                                .Where(cfg => cfg != null && seenGrantedAbilitySignatures.Add(JsonSerializer.Serialize(cfg, ReplayExecutionState.Json)))
                                .ToList();

                            if (dedupedConfigs.Count > 0)
                            {
                                filteredEffects.Add(new AbilityBuffEffect(dedupedConfigs));
                            }
                        }
                        else
                        {
                            filteredEffects.Add(effect);
                        }
                    }

                    if (filteredEffects.Count == 0)
                    {
                        continue;
                    }

                    var node = JsonNode.Parse(rawElement.GetRawText()) as JsonObject;
                    if (node == null)
                    {
                        continue;
                    }

                    node["effects"] = JsonSerializer.SerializeToNode(filteredEffects, ReplayExecutionState.Json);
                    extractedBuffNodes.Add(node);
                }
            }

            var result = new DomainOperationResult { Success = true };

            // Transfer exact shared-pool copy holdings from the 3 materials to the synthesized triple card BEFORE retiring them.
            var aggregatedPoolHoldings = new Dictionary<int, int>();
            foreach (var material in materials)
            {
                foreach (var (poolCardId, heldCount) in material.MiragePoolHoldings)
                {
                    if (poolCardId > 0 && heldCount > 0)
                    {
                        aggregatedPoolHoldings[poolCardId] = aggregatedPoolHoldings.GetValueOrDefault(poolCardId) + heldCount;
                    }
                }
                material.MiragePoolHoldings.Clear();
            }

            // Contract 3.2 Step 2: Retire all 3 material cards via RetireCardFromPlay
            // (NO CardSoldCompletedEvent, NO gold refund, NO Graveyard/Shadows/Destroy/LastWords).
            foreach (var material in materials)
            {
                var retireResult = MirageCardRetirement.RetireCardFromPlay(material, player, context);
                if (!retireResult.Success)
                {
                    return retireResult;
                }
                result.GeneratedEvents.AddRange(retireResult.GeneratedEvents);
            }

            // Contract 3.2 Step 3: Create a single new CardInstance in CardZone.Hand with 2x base stats and IsMirageTriple = true
            var baseDefinitionId = materials[0].BaseDefinitionId;
            var baseDef = _gameDataProvider.GetCardDefinition(baseDefinitionId) ?? materials[0].CurrentDefinition;
            var tripleCard = cardFactory.CreateAndRegisterNewCard(gameState, baseDefinitionId, player.PlayerId, CardZone.Hand);
            tripleCard.IsMirageTriple = true;
            tripleCard.IsMirageRecruited = true;
            tripleCard.MiragePoolHoldings = aggregatedPoolHoldings;
            tripleCard.CostOverride = 0;
            tripleCard.BaseAttackOverride = 2 * (baseDef?.Attack ?? 0);
            tripleCard.BaseHealthOverride = 2 * Math.Max(1, baseDef?.Health ?? 1);
            // Remain unevolved in hand; playing the triple evolves it after summoning without another stat bonus.

            synthesizedCard = tripleCard;

            // Contract 3.2 Step 4: Re-apply extracted permanent buffs onto the new triple card and grant 1 MirageDiscoverTicket
            foreach (var buffNode in extractedBuffNodes)
            {
                buffNode["id"] = Guid.NewGuid();
                buffNode["targetCardInstanceId"] = tripleCard.InstanceId;
                buffNode["sourceCardInstanceId"] = tripleCard.InstanceId;
                buffNode["sourceAbilityId"] = Guid.Empty;
                buffNode["summaryRootAbilityId"] = null;
                buffNode["isActive"] = true;
                buffNode["isAuraGenerated"] = false;

                var remappedElement = JsonSerializer.SerializeToElement(buffNode, ReplayExecutionState.Json);
                var migratedBuff = ReplayExecutionState.RestoreBuff(remappedElement);
                migratedBuff.IsAuraGenerated = false;
                result.GeneratedEvents.Add(new BuffAppliedEvent(migratedBuff, tripleCard, tripleCard));
            }

            var targetTier = Math.Clamp(player.Mirage.TavernTier + 1, 1, gameState.Mirage.Rules.MaxTavernTier);
            var ticket = CreateDiscoverTicket(gameState.Mirage, targetTier, gameState.Mirage.Rules, allowAboveMaxTier: false);
            if (ticket != null)
            {
                player.Mirage.PendingTickets.Add(ticket);
            }
            grantedTicket = ticket;

            var auraRefreshResult = MirageCardRetirement.RefreshAllBoardAuras(context, "mirage_triple_synthesized");
            if (!auraRefreshResult.Success)
            {
                return auraRefreshResult;
            }
            result.GeneratedEvents.AddRange(auraRefreshResult.GeneratedEvents);
            result.GeneratedEvents.Add(new HandUpdatedEvent(player, player.Hand.ToList()));

            return result;
        }

        public MirageDiscoverTicket? CreateDiscoverTicket(
            MirageMatchState? mirage,
            int targetTier,
            MirageRuleConfig rules,
            bool allowAboveMaxTier = false)
        {
            var effectiveMaxTier = Math.Max(1, rules.MaxTavernTier);
            var resolvedTier = allowAboveMaxTier
                ? Math.Max(1, targetTier)
                : Math.Clamp(targetTier, 1, effectiveMaxTier);

            var exactEntries = _shopService.GetPoolForExactTier(resolvedTier, effectiveMaxTier, allowAboveMaxTier)
                .GroupBy(c => c.CardDefinitionId)
                .Select(g => g.First())
                .ToList();

            var candidateIds = new List<int>(3);
            if (mirage != null)
            {
                PickWeightedDistinctWithReservation(mirage, exactEntries, 3, candidateIds);

                if (candidateIds.Count < 3 && !allowAboveMaxTier)
                {
                    var fallbackEntries = _shopService.GetPoolUpToTier(resolvedTier, effectiveMaxTier)
                        .Where(c => !candidateIds.Contains(c.CardDefinitionId))
                        .GroupBy(c => c.CardDefinitionId)
                        .Select(g => g.First())
                        .ToList();
                    PickWeightedDistinctWithReservation(mirage, fallbackEntries, 3 - candidateIds.Count, candidateIds);
                }
            }
            else
            {
                var exactIds = exactEntries.Select(e => e.CardDefinitionId).ToList();
                PickRandomDistinct(exactIds, 3, candidateIds);
                if (candidateIds.Count < 3 && !allowAboveMaxTier)
                {
                    var fallbackIds = _shopService.GetPoolUpToTier(resolvedTier, effectiveMaxTier)
                        .Select(c => c.CardDefinitionId)
                        .Where(id => !candidateIds.Contains(id))
                        .Distinct()
                        .ToList();
                    PickRandomDistinct(fallbackIds, 3 - candidateIds.Count, candidateIds);
                }
            }

            if (candidateIds.Count == 0)
            {
                return null;
            }

            return new MirageDiscoverTicket
            {
                TicketId = Guid.NewGuid(),
                TargetTier = resolvedTier,
                CandidateCardDefinitionIds = candidateIds
            };
        }

        private void PickWeightedDistinctWithReservation(
            MirageMatchState mirage,
            List<MirageCardCatalogEntry> sourceEntries,
            int count,
            List<int> destination)
        {
            if (sourceEntries.Count == 0 || count <= 0)
            {
                return;
            }

            var remainingEntries = new List<MirageCardCatalogEntry>(sourceEntries);
            for (int i = 0; i < count && remainingEntries.Count > 0; i++)
            {
                if (!_shopService.TryDrawWeightedFromPool(mirage, remainingEntries, out var picked))
                {
                    break;
                }

                destination.Add(picked.CardDefinitionId);
                remainingEntries.RemoveAll(e => e.CardDefinitionId == picked.CardDefinitionId);
            }
        }

        private static void PickRandomDistinct(List<int> pool, int count, List<int> destination)
        {
            if (pool.Count == 0 || count <= 0)
            {
                return;
            }

            var working = new List<int>(pool);
            var take = Math.Min(count, working.Count);
            for (int i = 0; i < take; i++)
            {
                int idx = Random.Shared.Next(i, working.Count);
                (working[i], working[idx]) = (working[idx], working[i]);
                destination.Add(working[i]);
            }
        }
    }
}
