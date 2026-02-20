using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Dtos;

public record DeckCardEntryDto
{
    public int CardDefinitionId { get; init; }
    public int Count { get; init; }
}

public record DeckSummaryDto
{
    public Guid DeckId { get; init; }
    public string Name { get; init; } = string.Empty;
    public CardClass Class { get; init; } = CardClass.Neutral;
    public DeckEnvironment Environment { get; init; } = DeckEnvironment.Open;
    public int CardCount { get; init; }
    public int TotalGames { get; init; }
    public int TotalWins { get; init; }
    public int GoingFirstGames { get; init; }
    public int GoingFirstWins { get; init; }
    public int GoingSecondGames { get; init; }
    public int GoingSecondWins { get; init; }
    public DateTime UpdatedAt { get; init; }
    public bool IsGuest { get; init; }
}

public record DeckDetailDto : DeckSummaryDto
{
    public List<DeckCardEntryDto> Cards { get; init; } = new();
}

public record CreateDeckRequest
{
    public string Name { get; init; } = string.Empty;
    public CardClass Class { get; init; } = CardClass.Neutral;
    public DeckEnvironment? Environment { get; init; }
    public List<DeckCardEntryDto>? Cards { get; init; }
}

public record UpdateDeckRequest
{
    public string? Name { get; init; }
    public CardClass? Class { get; init; }
    public DeckEnvironment? Environment { get; init; }
    public List<DeckCardEntryDto>? Cards { get; init; }
}

public record ImportDeckCodeRequest
{
    public string Code { get; init; } = string.Empty;
    public string? Name { get; init; }
    public Guid? TargetDeckId { get; init; }
    public DeckEnvironment? Environment { get; init; }
}

public record DeckAvailabilityDto
{
    public bool HasDeck { get; init; }
}
