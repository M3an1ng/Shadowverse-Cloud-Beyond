using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    public sealed record MirageCardCatalogEntry(
        int CardDefinitionId,
        int TavernTier,
        int BuyCost,
        string TripleGroupId);

    public interface IMirageShopService
    {
        MirageCardCatalogEntry? GetCatalogEntry(int cardDefinitionId);
        IReadOnlyList<MirageCardCatalogEntry> GetPoolUpToTier(int maxTier, int maxTavernTier = MirageRuleConfig.DefaultMaxTavernTier);
        IReadOnlyList<MirageCardCatalogEntry> GetPoolForExactTier(int tier, int maxTavernTier = MirageRuleConfig.DefaultMaxTavernTier, bool allowAboveMaxTier = false);
        void InitializeSharedCardPool(MirageMatchState mirage);
        bool IsManagedPoolCard(MirageMatchState? mirage, int cardDefinitionId);
        bool TryReserveFromPool(MirageMatchState mirage, int cardDefinitionId, int count = 1);
        void ReturnCopiesToPool(MirageMatchState? mirage, int cardDefinitionId, int count = 1);
        void ReleaseCardInstancePoolHoldings(GameState? gameState, CardInstance? card);
        void ReturnEliminatedPlayerCardsToPool(GameState? gameState, Guid playerId);
        bool TryDrawWeightedFromPool(MirageMatchState mirage, IReadOnlyList<MirageCardCatalogEntry> candidates, out MirageCardCatalogEntry picked);
        List<MirageShopOffer> RollShopOffers(MirageMatchState mirage, int tavernTier, int? countOverride = null);
        void RefreshShopForNewRound(PlayerState player, MirageMatchState mirage);
        void ForceRefreshShop(PlayerState player, MirageMatchState mirage);
    }

    public sealed class MirageShopService : IMirageShopService
    {
        private readonly IGameDataProvider _gameDataProvider;

        public MirageShopService(IGameDataProvider gameDataProvider)
        {
            _gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
        }

        public MirageCardCatalogEntry? GetCatalogEntry(int cardDefinitionId)
        {
            var def = _gameDataProvider.GetCardDefinition(cardDefinitionId);
            if (!IsEligibleMirageShopFollower(def))
            {
                return null;
            }

            var tier = ResolveTavernTier(def!);
            return new MirageCardCatalogEntry(
                def!.Id,
                tier,
                ResolveBuyCost(def),
                def.Id.ToString());
        }

        public IReadOnlyList<MirageCardCatalogEntry> GetPoolUpToTier(int maxTier, int maxTavernTier = MirageRuleConfig.DefaultMaxTavernTier)
        {
            var effectiveMaxTier = Math.Max(1, maxTavernTier);
            var clampedTier = Math.Clamp(maxTier, 1, effectiveMaxTier);
            var catalog = BuildCatalog();
            return catalog.Where(c => c.TavernTier <= clampedTier).ToList();
        }

        public IReadOnlyList<MirageCardCatalogEntry> GetPoolForExactTier(
            int tier,
            int maxTavernTier = MirageRuleConfig.DefaultMaxTavernTier,
            bool allowAboveMaxTier = false)
        {
            var effectiveMaxTier = Math.Max(1, maxTavernTier);
            var targetTier = allowAboveMaxTier
                ? Math.Max(1, tier)
                : Math.Clamp(tier, 1, effectiveMaxTier);
            var catalog = BuildCatalog();
            return catalog.Where(c => c.TavernTier == targetTier).ToList();
        }

        public void InitializeSharedCardPool(MirageMatchState mirage)
        {
            if (mirage == null) throw new ArgumentNullException(nameof(mirage));

            lock (mirage.SharedPoolSyncRoot)
            {
                mirage.SharedCardPool.Clear();
                mirage.InitialCardPoolCaps.Clear();
                mirage.ReturnedEliminatedPlayerIds.Clear();

                var catalog = BuildCatalog();
                foreach (var entry in catalog)
                {
                    var initialCopies = mirage.Rules.GetInitialPoolCopiesForTier(entry.TavernTier);
                    mirage.InitialCardPoolCaps[entry.CardDefinitionId] = initialCopies;
                    mirage.SharedCardPool[entry.CardDefinitionId] = initialCopies;
                }
            }
        }

        private void EnsureSharedCardPoolInitialized(MirageMatchState mirage)
        {
            lock (mirage.SharedPoolSyncRoot)
            {
                if (mirage.InitialCardPoolCaps.Count == 0 && mirage.SharedCardPool.Count == 0)
                {
                    InitializeSharedCardPool(mirage);
                }
            }
        }

        public bool IsManagedPoolCard(MirageMatchState? mirage, int cardDefinitionId)
        {
            if (mirage == null || cardDefinitionId <= 0)
            {
                return false;
            }

            lock (mirage.SharedPoolSyncRoot)
            {
                EnsureSharedCardPoolInitialized(mirage);
                return mirage.InitialCardPoolCaps.ContainsKey(cardDefinitionId);
            }
        }

        public bool TryReserveFromPool(MirageMatchState mirage, int cardDefinitionId, int count = 1)
        {
            if (mirage == null) throw new ArgumentNullException(nameof(mirage));
            if (cardDefinitionId <= 0 || count <= 0)
            {
                return false;
            }

            lock (mirage.SharedPoolSyncRoot)
            {
                EnsureSharedCardPoolInitialized(mirage);
                var remaining = mirage.SharedCardPool.GetValueOrDefault(cardDefinitionId);
                if (remaining < count)
                {
                    return false;
                }

                mirage.SharedCardPool[cardDefinitionId] = remaining - count;
                return true;
            }
        }

        public void ReturnCopiesToPool(MirageMatchState? mirage, int cardDefinitionId, int count = 1)
        {
            if (mirage == null || cardDefinitionId <= 0 || count <= 0)
            {
                return;
            }

            lock (mirage.SharedPoolSyncRoot)
            {
                if (!mirage.InitialCardPoolCaps.TryGetValue(cardDefinitionId, out var cap))
                {
                    return;
                }

                var current = mirage.SharedCardPool.GetValueOrDefault(cardDefinitionId);
                mirage.SharedCardPool[cardDefinitionId] = Math.Clamp(current + count, 0, cap);
            }
        }

        public void ReleaseCardInstancePoolHoldings(GameState? gameState, CardInstance? card)
        {
            if (gameState == null
                || gameState.IsCombatSandbox
                || gameState.Mirage == null
                || card == null
                || card.MiragePoolHoldings.Count == 0)
            {
                return;
            }

            lock (gameState.Mirage.SharedPoolSyncRoot)
            {
                foreach (var (poolCardId, heldCount) in card.MiragePoolHoldings)
                {
                    ReturnCopiesToPool(gameState.Mirage, poolCardId, heldCount);
                }

                card.MiragePoolHoldings.Clear();
            }
        }

        public void ReturnEliminatedPlayerCardsToPool(GameState? gameState, Guid playerId)
        {
            if (gameState == null
                || gameState.IsCombatSandbox
                || gameState.Mode != GameMode.Mirage
                || gameState.Mirage == null
                || playerId == Guid.Empty)
            {
                return;
            }

            var mirage = gameState.Mirage;
            lock (mirage.SharedPoolSyncRoot)
            {
                if (!mirage.ReturnedEliminatedPlayerIds.Add(playerId))
                {
                    return;
                }

                var player = gameState.GetPlayerState(playerId);
                if (player?.Mirage != null)
                {
                    foreach (var offer in player.Mirage.ShopOffers)
                    {
                        if (offer != null)
                        {
                            ReturnCopiesToPool(mirage, offer.CardDefinitionId, 1);
                        }
                    }
                    player.Mirage.ShopOffers.Clear();

                    foreach (var ticket in player.Mirage.PendingTickets)
                    {
                        if (ticket?.CandidateCardDefinitionIds == null) continue;
                        foreach (var candidateId in ticket.CandidateCardDefinitionIds)
                        {
                            ReturnCopiesToPool(mirage, candidateId, 1);
                        }
                    }
                    player.Mirage.PendingTickets.Clear();
                }

                var ownedCards = new HashSet<CardInstance>(ReferenceEqualityComparer.Instance);
                if (player != null)
                {
                    foreach (var c in player.Field.Concat(player.Hand))
                    {
                        if (c != null) ownedCards.Add(c);
                    }
                }

                foreach (var c in gameState.AllCards)
                {
                    if (c != null && c.OwnerPlayerId == playerId && c.MiragePoolHoldings.Count > 0)
                    {
                        ownedCards.Add(c);
                    }
                }

                foreach (var card in ownedCards)
                {
                    ReleaseCardInstancePoolHoldings(gameState, card);
                }
            }
        }

        public bool TryDrawWeightedFromPool(
            MirageMatchState mirage,
            IReadOnlyList<MirageCardCatalogEntry> candidates,
            out MirageCardCatalogEntry picked)
        {
            picked = null!;
            if (mirage == null || candidates == null || candidates.Count == 0)
            {
                return false;
            }

            lock (mirage.SharedPoolSyncRoot)
            {
                EnsureSharedCardPoolInitialized(mirage);
                long totalWeight = 0;
                for (int i = 0; i < candidates.Count; i++)
                {
                    var remaining = mirage.SharedCardPool.GetValueOrDefault(candidates[i].CardDefinitionId);
                    if (remaining > 0)
                    {
                        totalWeight += remaining;
                    }
                }

                if (totalWeight <= 0)
                {
                    return false;
                }

                long roll = Random.Shared.NextInt64(totalWeight);
                long cumulative = 0;
                for (int i = 0; i < candidates.Count; i++)
                {
                    var entry = candidates[i];
                    var remaining = mirage.SharedCardPool.GetValueOrDefault(entry.CardDefinitionId);
                    if (remaining <= 0)
                    {
                        continue;
                    }

                    cumulative += remaining;
                    if (roll < cumulative)
                    {
                        mirage.SharedCardPool[entry.CardDefinitionId] = remaining - 1;
                        picked = entry;
                        return true;
                    }
                }

                return false;
            }
        }

        public List<MirageShopOffer> RollShopOffers(MirageMatchState mirage, int tavernTier, int? countOverride = null)
        {
            if (mirage == null) throw new ArgumentNullException(nameof(mirage));
            lock (mirage.SharedPoolSyncRoot)
            {
                EnsureSharedCardPoolInitialized(mirage);

                var rules = mirage.Rules;
                var slotCount = Math.Max(0, countOverride ?? rules.GetShopSlotCount(tavernTier));
                if (slotCount == 0)
                {
                    return new List<MirageShopOffer>();
                }

                var pool = GetPoolUpToTier(tavernTier, rules.MaxTavernTier);
                if (pool.Count == 0)
                {
                    return new List<MirageShopOffer>();
                }

                var offers = new List<MirageShopOffer>(slotCount);
                for (int i = 0; i < slotCount; i++)
                {
                    if (!TryDrawWeightedFromPool(mirage, pool, out var picked))
                    {
                        break;
                    }

                    var def = _gameDataProvider.GetCardDefinition(picked.CardDefinitionId);
                    var effectiveBuyCost = def != null
                        ? ResolveBuyCost(def, rules)
                        : (rules.BuyCost > 0 ? rules.BuyCost : picked.BuyCost);

                    offers.Add(new MirageShopOffer
                    {
                        OfferId = Guid.NewGuid(),
                        CardDefinitionId = picked.CardDefinitionId,
                        TavernTier = picked.TavernTier,
                        BuyCost = effectiveBuyCost
                    });
                }

                return offers;
            }
        }

        public void RefreshShopForNewRound(PlayerState player, MirageMatchState mirage)
        {
            if (player?.Mirage == null || mirage == null)
            {
                return;
            }

            lock (mirage.SharedPoolSyncRoot)
            {
                EnsureSharedCardPoolInitialized(mirage);
                var targetSlots = mirage.Rules.GetShopSlotCount(player.Mirage.TavernTier);
                if (player.Mirage.IsShopFrozen)
                {
                    var missingSlots = Math.Max(0, targetSlots - player.Mirage.ShopOffers.Count);
                    if (missingSlots > 0)
                    {
                        var refill = RollShopOffers(mirage, player.Mirage.TavernTier, missingSlots);
                        player.Mirage.ShopOffers.AddRange(refill);
                    }

                    player.Mirage.IsShopFrozen = false;
                    return;
                }

                ReleaseShopOffersToPool(player.Mirage.ShopOffers, mirage);
                player.Mirage.ShopOffers = RollShopOffers(mirage, player.Mirage.TavernTier, targetSlots);
            }
        }

        public void ForceRefreshShop(PlayerState player, MirageMatchState mirage)
        {
            if (player?.Mirage == null || mirage == null)
            {
                return;
            }

            lock (mirage.SharedPoolSyncRoot)
            {
                EnsureSharedCardPoolInitialized(mirage);
                var targetSlots = mirage.Rules.GetShopSlotCount(player.Mirage.TavernTier);
                player.Mirage.IsShopFrozen = false;
                ReleaseShopOffersToPool(player.Mirage.ShopOffers, mirage);
                player.Mirage.ShopOffers = RollShopOffers(mirage, player.Mirage.TavernTier, targetSlots);
            }
        }

        private void ReleaseShopOffersToPool(List<MirageShopOffer>? offers, MirageMatchState mirage)
        {
            if (offers == null || offers.Count == 0)
            {
                return;
            }

            foreach (var offer in offers)
            {
                if (offer != null)
                {
                    ReturnCopiesToPool(mirage, offer.CardDefinitionId, 1);
                }
            }

            offers.Clear();
        }

        private List<MirageCardCatalogEntry> BuildCatalog()
        {
            var allDefs = _gameDataProvider is IEnvironmentScopedGameDataProvider scopedProvider
                ? scopedProvider.GetMirageCardDefinitions()
                : _gameDataProvider.GetAllCardDefinitions().Values;

            return allDefs
                .Where(IsEligibleMirageShopFollower)
                .Select(d => new MirageCardCatalogEntry(
                    d.Id,
                    ResolveTavernTier(d),
                    ResolveBuyCost(d),
                    d.Id.ToString()))
                .ToList();
        }

        private static bool IsEligibleMirageShopFollower(CardDefinition? def)
        {
            return def != null
                && def.IsMirageCard
                && def.Id > 0
                && def.IsCollectible
                && def.Type == CardType.Follower
                && def.Rarity != CardRarity.Special
                && (def.Attack ?? 0) > 0
                && (def.Health ?? 0) > 0;
        }

        public static int ResolveTavernTier(CardDefinition def)
        {
            return Math.Max(1, def.Cost);
        }

        public static int ResolveBuyCost(CardDefinition def, MirageRuleConfig? rules = null)
        {
            if (def.MirageBuyCost.HasValue)
            {
                return Math.Max(0, def.MirageBuyCost.Value);
            }

            return rules is { BuyCost: > 0 } ? rules.BuyCost : MirageRuleConfig.DefaultBuyCost;
        }
    }
}
