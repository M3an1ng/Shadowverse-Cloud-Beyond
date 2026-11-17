using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

public partial class MatchService
{
    public MatchRoom OpenTournamentDuel(
        Guid tournamentMatchId, Guid gameId, TournamentEntry one, TournamentEntry two,
        TournamentEntryDeck oneDeck, TournamentEntryDeck twoDeck, DeckEnvironment environment, DeckRule rule)
    {
        var id = gameId.ToString("D");
        lock (_creationGate)
        {
            if (_rooms.TryGetValue(id, out var previous))
            {
                lock (previous)
                {
                    if (previous.TournamentMatchId != tournamentMatchId)
                        throw new InvalidOperationException("Tournament duel id belongs to a different match.");
                    if (previous.Status != MatchStatus.Closed && !IsExpired(previous))
                    {
                        previous.ExpiresAt = DateTime.UtcNow.Add(previous.RoomTtl);
                        return Clone(previous);
                    }
                    CloseInternal(id, "赛事对战连接已过期。");
                }
            }

            var now = DateTime.UtcNow;
            var room = new MatchRoom
            {
                RoomId = id,
                ActiveGameId = id,
                TournamentMatchId = tournamentMatchId,
                HostPlayerId = one.UserId,
                GuestPlayerId = two.UserId,
                HostToken = GenerateToken(),
                GuestToken = GenerateToken(),
                HostReady = true,
                GuestReady = true,
                IsPublic = false,
                Status = MatchStatus.Started,
                Round = 1,
                RoomTtl = _defaultTtl,
                CreatedAt = now,
                ExpiresAt = now.Add(_defaultTtl),
                HostDeckSelection = TournamentSelection(oneDeck, environment, rule),
                GuestDeckSelection = TournamentSelection(twoDeck, environment, rule)
            };
            _gameToRoom[id] = id;
            _rooms[id] = room;
            return Clone(room);
        }
    }

    private static MatchDeckSelection TournamentSelection(
        TournamentEntryDeck deck, DeckEnvironment environment, DeckRule rule) => new()
    {
        DeckCode = deck.DeckCode,
        TournamentCardIds = deck.ReadCards().SelectMany(card => Enumerable.Repeat(card.CardDefinitionId, card.Count)).ToArray(),
        DeckName = deck.Name,
        DeckClass = deck.Class,
        SecondaryClass = deck.SecondaryClass,
        Environment = environment,
        Rule = rule
    };
}
