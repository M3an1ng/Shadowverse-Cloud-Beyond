using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos
{
    public record MirageShopOfferDto
    {
        public Guid OfferId { get; init; }
        public int CardDefinitionId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string? ArtPreview { get; init; }
        public int Attack { get; init; }
        public int Health { get; init; }
        public int TavernTier { get; init; }
        public int BuyCost { get; init; }
        public int RemainingPoolCopies { get; init; }
        public string? TripleGroupId { get; init; }
        public string? Class { get; init; }
        public string? Type { get; init; }
    }

    public record MirageDiscoverTicketDto
    {
        public Guid TicketId { get; init; }
        public int TargetTier { get; init; }
        public List<int> CandidateCardDefinitionIds { get; init; } = new();
        public List<MirageShopOfferDto> Candidates { get; init; } = new();
    }

    public record MiragePlayerStateDto
    {
        public int TavernTier { get; init; } = 1;
        public int MaxTavernTier { get; init; } = 6;
        public int UpgradeCost { get; init; } = 5;
        public bool CanUpgradeTier { get; init; } = true;
        public bool IsShopFrozen { get; init; }
        public bool IsReady { get; init; }
        public int HardGoldCap { get; init; } = 10;
        public int RefreshCost { get; init; } = 1;
        public int SellRefund { get; init; } = 1;
        public bool HasSelectedLeader { get; init; }
        public List<int> LeaderCandidateCardIds { get; init; } = new();
        public List<MirageShopOfferDto> LeaderCandidates { get; init; } = new();
        public List<MirageShopOfferDto> ShopOffers { get; init; } = new();
        public List<MirageDiscoverTicketDto> PendingTickets { get; init; } = new();
    }

    public record MirageMatchStateDto
    {
        public string? TournamentRoomId { get; init; }
        public int? TournamentSeatIndex { get; init; }
        public bool IsNextRoundGateClosed { get; init; }
        public int RoundNumber { get; init; } = 1;
        public List<Guid> ReadyPlayerIds { get; init; } = new();
        public Guid? ActiveCombatId { get; init; }
        public List<Guid> CommittedCombatIds { get; init; } = new();
        public Guid? LastCombatId { get; init; }
        public int? LastCombatRoundNumber { get; init; }
        public Guid? LastCombatWinnerPlayerId { get; init; }
        public Guid? LastCombatLoserPlayerId { get; init; }
        public int LastCombatLeaderDamage { get; init; }
        public int LastCombatStepCount { get; init; }
        public int HardGoldCap { get; init; } = 10;
        public int MaxTavernTier { get; init; } = 6;
        public Dictionary<int, int> SharedCardPool { get; init; } = new();
        public Dictionary<int, int> InitialCardPoolCaps { get; init; } = new();
    }
}
