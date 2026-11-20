using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Dtos;

public enum TournamentMatchPhase
{
    WaitingForLineups, Ban, Pick, Battle, Completed
}

public sealed record TournamentMatchDeckDto(
    Guid Id, int Slot, string Name, CardClass Class, CardClass? SecondaryClass, bool Banned, bool HasWon);

public sealed record TournamentMatchSeatDto(
    Guid EntryId, string PlayerName, string? AvatarUrl, bool IsSelf, int Wins, bool BanLocked, bool PickLocked,
    TournamentMatchDeckDto[] Decks);

public sealed record TournamentGameResultDto(
    int Number, Guid EntryOneDeckId, Guid EntryTwoDeckId, Guid? WinnerEntryId, Guid? FirstPlayerEntryId);

public sealed record TournamentMatchRoomDto(
    Guid Id, Guid TournamentId, string TournamentName, TournamentStageKind Stage, int StageNumber, int RoundNumber,
    int PairingNumber, TournamentBracket Bracket, TournamentMatchFormat MatchFormat, DeckRule DeckRule, TournamentMatchPhase Phase,
    TournamentMatchSeatDto One, TournamentMatchSeatDto Two, int GameNumber, Guid? ActiveGameId,
    Guid? MyBan, Guid? MyPick, Guid? WinnerEntryId, TournamentGameResultDto[] Results);

public sealed record TournamentPairingDto(
    Guid Id, int StageNumber, int RoundNumber, int PairingNumber, TournamentBracket Bracket, string PlayerOne, string? PlayerTwo,
    Guid EntryOneId, Guid? EntryTwoId, bool IsMine, Guid? WinnerEntryId, bool IsComplete, int OneWins, int TwoWins);

public sealed record TournamentDeckChoiceRequest(Guid DeckId);
public sealed record EnterTournamentGameRequest(Guid GameId);
public sealed record TournamentGameSeatDto(Guid GameId, Guid PlayerId, string Token, string Role);
