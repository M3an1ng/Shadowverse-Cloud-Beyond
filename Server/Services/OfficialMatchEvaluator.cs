using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

/// <summary>
/// Shared official match evaluator.
/// Officiality semantics are aligned with coin reward eligibility.
/// </summary>
public sealed class OfficialMatchEvaluator : IOfficialMatchEvaluator
{
    private readonly ApplicationDbContext _db;

    public OfficialMatchEvaluator(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<OfficialMatchEvaluation> EvaluateAsync(
        GameState gameState,
        GameEndEvent gameEndEvent,
        CancellationToken cancellationToken)
    {
        var participants = new[]
        {
            gameState.Player1,
            gameState.Player2
        };

        var participantIds = participants
            .Where(participant => participant != null && participant.PlayerId != Guid.Empty)
            .Select(participant => participant!.PlayerId)
            .Distinct()
            .ToList();

        var persistedUserIds = participantIds.Count == 0
            ? new HashSet<Guid>()
            : new HashSet<Guid>(await _db.Users
                .AsNoTracking()
                .Where(user => participantIds.Contains(user.Id))
                .Select(user => user.Id)
                .ToListAsync(cancellationToken));

        var nonOfficialReason = DetermineNonOfficialReason(gameState, participants, persistedUserIds, gameEndEvent);
        return new OfficialMatchEvaluation(string.IsNullOrEmpty(nonOfficialReason), nonOfficialReason);
    }

    private static string? DetermineNonOfficialReason(
        GameState gameState,
        IReadOnlyList<PlayerState?> participants,
        IReadOnlySet<Guid> persistedUserIds,
        GameEndEvent gameEndEvent)
    {
        // Single-player modes (AI/local guest) are non-official.
        if (participants.Any(participant => participant?.IsAI == true))
        {
            return "SinglePlayerMode";
        }

        if (participants.Any(participant =>
                participant != null &&
                participant.PlayerId != Guid.Empty &&
                !persistedUserIds.Contains(participant.PlayerId)))
        {
            return "SinglePlayerMode";
        }

        // Incomplete decks are non-official.
        if (participants.Any(participant =>
                participant == null ||
                participant.InitialDeck == null ||
                participant.InitialDeck.Count < GameConstants.Cards.MaxDeckSize))
        {
            return "IncompleteDeck";
        }

        // Early surrender before super evolution turn is non-official.
        if (IsSurrenderReason(gameEndEvent.Reason) &&
            IsBeforeSuperEvolutionTurn(gameState, gameEndEvent.LoserId))
        {
            return "EarlySurrender";
        }

        return null;
    }

    private static bool IsBeforeSuperEvolutionTurn(GameState gameState, Guid surrenderPlayerId)
    {
        var surrenderPlayer = gameState.GetPlayerState(surrenderPlayerId);
        if (surrenderPlayer == null)
        {
            return false;
        }

        var superEvolutionStartTurn = surrenderPlayer.IsFirst
            ? GameConstants.SuperEvolution.FirstPlayerSuperEvolutionStartTurn
            : GameConstants.SuperEvolution.SecondPlayerSuperEvolutionStartTurn;

        return surrenderPlayer.TurnNumber < superEvolutionStartTurn;
    }

    private static bool IsSurrenderReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        return reason.Contains("Surrender", StringComparison.OrdinalIgnoreCase)
               || reason.Contains("投降", StringComparison.Ordinal);
    }
}
