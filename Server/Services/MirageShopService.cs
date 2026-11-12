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
        IReadOnlyList<MirageCardCatalogEntry> GetPoolForExactTier(int tier, int maxTavernTier = MirageRuleConfig.DefaultMaxTavernTier);
        List<MirageShopOffer> RollShopOffers(int tavernTier, MirageRuleConfig rules, int? countOverride = null);
        void RefreshShopForNewRound(PlayerState player, MirageRuleConfig rules);
        void ForceRefreshShop(PlayerState player, MirageRuleConfig rules);
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
                MirageRuleConfig.DefaultBuyCost,
                def.Id.ToString());
        }

        public IReadOnlyList<MirageCardCatalogEntry> GetPoolUpToTier(int maxTier, int maxTavernTier = MirageRuleConfig.DefaultMaxTavernTier)
        {
            var clampedTier = Math.Clamp(maxTier, 1, maxTavernTier);
            var catalog = BuildCatalog(maxTavernTier);
            var filtered = catalog.Where(c => c.TavernTier <= clampedTier).ToList();
            return filtered.Count > 0 ? filtered : catalog;
        }

        public IReadOnlyList<MirageCardCatalogEntry> GetPoolForExactTier(int tier, int maxTavernTier = MirageRuleConfig.DefaultMaxTavernTier)
        {
            var clampedTier = Math.Clamp(tier, 1, maxTavernTier);
            var catalog = BuildCatalog(maxTavernTier);
            var exact = catalog.Where(c => c.TavernTier == clampedTier).ToList();
            if (exact.Count > 0)
            {
                return exact;
            }

            return GetPoolUpToTier(clampedTier, maxTavernTier);
        }

        public List<MirageShopOffer> RollShopOffers(int tavernTier, MirageRuleConfig rules, int? countOverride = null)
        {
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
                var picked = pool[Random.Shared.Next(pool.Count)];
                offers.Add(new MirageShopOffer
                {
                    OfferId = Guid.NewGuid(),
                    CardDefinitionId = picked.CardDefinitionId,
                    TavernTier = picked.TavernTier,
                    BuyCost = rules.BuyCost > 0 ? rules.BuyCost : picked.BuyCost
                });
            }

            return offers;
        }

        public void RefreshShopForNewRound(PlayerState player, MirageRuleConfig rules)
        {
            if (player?.Mirage == null)
            {
                return;
            }

            var targetSlots = rules.GetShopSlotCount(player.Mirage.TavernTier);
            if (player.Mirage.IsShopFrozen)
            {
                var missingSlots = Math.Max(0, targetSlots - player.Mirage.ShopOffers.Count);
                if (missingSlots > 0)
                {
                    var refill = RollShopOffers(player.Mirage.TavernTier, rules, missingSlots);
                    player.Mirage.ShopOffers.AddRange(refill);
                }

                player.Mirage.IsShopFrozen = false;
                return;
            }

            player.Mirage.ShopOffers = RollShopOffers(player.Mirage.TavernTier, rules, targetSlots);
        }

        public void ForceRefreshShop(PlayerState player, MirageRuleConfig rules)
        {
            if (player?.Mirage == null)
            {
                return;
            }

            var targetSlots = rules.GetShopSlotCount(player.Mirage.TavernTier);
            player.Mirage.IsShopFrozen = false;
            player.Mirage.ShopOffers = RollShopOffers(player.Mirage.TavernTier, rules, targetSlots);
        }

        private List<MirageCardCatalogEntry> BuildCatalog(int maxTavernTier)
        {
            var allDefs = _gameDataProvider is IEnvironmentScopedGameDataProvider scopedProvider
                ? scopedProvider.GetMirageCardDefinitions()
                : _gameDataProvider.GetAllCardDefinitions().Values;

            return allDefs
                .Where(IsEligibleMirageShopFollower)
                .Select(d => new MirageCardCatalogEntry(
                    d.Id,
                    Math.Clamp(ResolveTavernTier(d), 1, maxTavernTier),
                    MirageRuleConfig.DefaultBuyCost,
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

        private static int ResolveTavernTier(CardDefinition def)
        {
            if (def.MirageTavernTier.HasValue)
            {
                return Math.Clamp(def.MirageTavernTier.Value, 1, MirageRuleConfig.DefaultMaxTavernTier);
            }

            return ComputeTavernTier(def);
        }

        private static int ComputeTavernTier(CardDefinition def)
        {
            if (def.Cost <= 1) return 1;
            if (def.Cost == 2) return 2;
            if (def.Cost == 3) return 3;
            if (def.Cost == 4) return 4;
            if (def.Cost <= 6) return 5;
            return 6;
        }
    }
}
