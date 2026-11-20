using System.Text.Json;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Entities;

public sealed class TournamentEntry
{
    public Guid Id { get; init; }
    public Guid TournamentId { get; init; }
    public Guid UserId { get; init; }
    public string PlayerName { get; init; } = string.Empty;
    public DateTime RegisteredAtUtc { get; init; }
    public int Revision { get; set; }
    public List<TournamentEntryDeck> Decks { get; } = [];

    public TournamentEntryDeck[] GetDecksForStage(int stageNumber, int count)
    {
        var lineupStage = Decks.Where(deck => deck.StageNumber <= stageNumber)
            .Select(deck => deck.StageNumber).DefaultIfEmpty().Max();
        return Decks.Where(deck => deck.StageNumber == lineupStage).OrderBy(deck => deck.Slot).Take(count).ToArray();
    }
}

public sealed class TournamentEntryDeck
{
    public Guid Id { get; init; }
    public Guid EntryId { get; init; }
    public int StageNumber { get; init; }
    public int Slot { get; init; }
    public Guid SourceDeckId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CardClass Class { get; set; }
    public CardClass? SecondaryClass { get; set; }
    public string DeckCode { get; set; } = string.Empty;
    public string CardsJson { get; set; } = "[]";
    public string? Archetype { get; set; }

    public TournamentDeckCard[] ReadCards() =>
        JsonSerializer.Deserialize<TournamentDeckCard[]>(CardsJson) ?? [];
}

public sealed class TournamentStageEntry
{
    public Guid TournamentId { get; init; }
    public int StageNumber { get; init; }
    public Guid EntryId { get; init; }
    public int Seed { get; init; }
    public bool CanPlay { get; set; } = true;
}

public sealed record TournamentDeckCard(int CardDefinitionId, string Name, int Cost, int Count);
