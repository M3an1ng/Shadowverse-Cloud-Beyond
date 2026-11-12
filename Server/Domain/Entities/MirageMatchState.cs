using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ShadowverseBeyond.Server.Domain.Entities
{
    public class MirageRuleConfig
    {
        public const int DefaultMaxTavernTier = 6;
        public const int DefaultHardGoldCap = 10;
        public const int DefaultSellRefund = 1;
        public const int DefaultBuyCost = 3;
        public const int DefaultRefreshCost = 1;
        public const int DefaultRecruitTimeoutSeconds = 75;
        public const int DefaultMaxCombatSteps = 100;

        public int MaxTavernTier { get; set; } = DefaultMaxTavernTier;
        public int HardGoldCap { get; set; } = DefaultHardGoldCap;
        public int SellRefund { get; set; } = DefaultSellRefund;
        public int BuyCost { get; set; } = DefaultBuyCost;
        public int RefreshCost { get; set; } = DefaultRefreshCost;
        public int RecruitTimeoutSeconds { get; set; } = DefaultRecruitTimeoutSeconds;
        public int MaxCombatSteps { get; set; } = DefaultMaxCombatSteps;

        public Dictionary<int, int> BaseUpgradeCostByTargetTier { get; set; } = new()
        {
            [2] = 5,
            [3] = 7,
            [4] = 8,
            [5] = 9,
            [6] = 10
        };

        public int GetBaseUpgradeCostForNextTier(int currentTier)
        {
            if (currentTier >= MaxTavernTier)
            {
                return 0;
            }

            var targetTier = currentTier + 1;
            return BaseUpgradeCostByTargetTier.TryGetValue(targetTier, out var cost) ? cost : 10;
        }

        public int GetShopSlotCount(int tier)
        {
            if (tier <= 1) return 3;
            if (tier <= 3) return 4;
            if (tier <= 5) return 5;
            return 6;
        }

        public int GetRoundBaseGold(int roundNumber)
        {
            return Math.Min(HardGoldCap, Math.Max(3, 2 + roundNumber));
        }
    }

    public class MirageShopOffer
    {
        public Guid OfferId { get; set; } = Guid.NewGuid();
        public int CardDefinitionId { get; set; }
        public int TavernTier { get; set; } = 1;
        public int BuyCost { get; set; } = MirageRuleConfig.DefaultBuyCost;
    }

    public class MirageDiscoverTicket
    {
        public Guid TicketId { get; set; } = Guid.NewGuid();
        public int TargetTier { get; set; } = 1;
        public List<int> CandidateCardDefinitionIds { get; set; } = new();
    }

    public class MiragePlayerState
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public int LastTriggeredAiRecruitRound { get; set; } = 0;

        public int TavernTier { get; set; } = 1;
        public int UpgradeCost { get; set; } = 5;
        public int HardGoldCap { get; set; } = MirageRuleConfig.DefaultHardGoldCap;
        public bool IsShopFrozen { get; set; } = false;
        public bool HasSelectedLeader { get; set; } = false;
        public List<int> LeaderCandidateCardIds { get; set; } = new();
        public List<MirageShopOffer> ShopOffers { get; set; } = new();
        public List<MirageDiscoverTicket> PendingTickets { get; set; } = new();
    }

    public class MirageMatchState
    {
        [JsonIgnore]
        public object RoundSyncLock { get; } = new();
        public int RoundNumber { get; set; } = 1;
        public int? ClosingRecruitRoundNumber { get; set; }
        public HashSet<Guid> ReadyPlayerIds { get; set; } = new();
        public Guid? ActiveCombatId { get; set; }
        public HashSet<Guid> CommittedCombatIds { get; set; } = new();
        public Guid? LastCombatId { get; set; }
        public int? LastCombatRoundNumber { get; set; }
        public Guid? LastCombatWinnerPlayerId { get; set; }
        public Guid? LastCombatLoserPlayerId { get; set; }
        public int LastCombatLeaderDamage { get; set; }
        public int LastCombatStepCount { get; set; }
        [JsonIgnore]
        public ShadowverseBeyond.Server.Dtos.MirageCombatReplayDto? LastCombatReplay { get; set; }
        [JsonIgnore]
        public Guid? NextCombatGhostId { get; set; }
        public MirageRuleConfig Rules { get; set; } = new();
    }
}
