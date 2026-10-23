using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

/// <summary>
/// Minimal meta-progression implementation (MVP):
/// - On game end, settles profile/rank/coins once per (GameId, UserId).
/// - Designed to be called from the game transaction pipeline (post-commit).
/// </summary>
public sealed class MetaProgressionService : IMetaProgressionService
{
    private const int WinCoinReward = 100;
    private const int LossCoinReward = 100;

    private readonly ApplicationDbContext _db;
    private readonly IEconomyService _economyService;
    private readonly ITransactionService _transactionService;
    private readonly IOfficialMatchEvaluator _officialMatchEvaluator;
    private readonly IQuestProgressService _questProgressService;
    private readonly ILogger<MetaProgressionService> _logger;

    public MetaProgressionService(
        ApplicationDbContext db,
        IEconomyService economyService,
        ITransactionService transactionService,
        IOfficialMatchEvaluator officialMatchEvaluator,
        IQuestProgressService questProgressService,
        ILogger<MetaProgressionService> logger)
    {
        _db = db;
        _economyService = economyService;
        _transactionService = transactionService;
        _officialMatchEvaluator = officialMatchEvaluator;
        _questProgressService = questProgressService;
        _logger = logger;
    }

    public async Task ApplyPostCommitAsync(
        GameContext gameContext,
        IReadOnlyList<IGameEvent> processedEvents,
        CancellationToken cancellationToken)
    {
        var gameEndEvent = processedEvents.LastOrDefault(e => e is GameEndEvent) as GameEndEvent;
        if (gameEndEvent == null)
        {
            return;
        }

        var gameId = gameContext.CurrentGameState.GameId;
        if (string.IsNullOrWhiteSpace(gameId))
        {
            _logger.LogWarning("[MetaProgression] GameId is empty. Skipping settlement.");
            return;
        }

        var participants = new[]
        {
            gameContext.CurrentGameState.Player1,
            gameContext.CurrentGameState.Player2
        };

        var officialMatch = await _officialMatchEvaluator.EvaluateAsync(
            gameContext.CurrentGameState,
            gameEndEvent,
            cancellationToken);
        var coinRewardEligible = officialMatch.IsOfficial;

        foreach (var participant in participants)
        {
            if (participant == null) continue;
            if (participant.IsAI) continue;

            await SettleUserOnceAsync(
                gameId,
                participant.PlayerId,
                gameContext.CurrentGameState.StartedAtUtc,
                winnerId: gameEndEvent.WinnerId,
                loserId: gameEndEvent.LoserId,
                reason: gameEndEvent.Reason,
                coinRewardEligible: coinRewardEligible,
                nonOfficialReason: officialMatch.NonOfficialReason,
                cancellationToken);
        }
    }

    private async Task SettleUserOnceAsync(
        string gameId,
        Guid userId,
        DateTime gameStartedAtUtc,
        Guid winnerId,
        Guid loserId,
        string? reason,
        bool coinRewardEligible,
        string? nonOfficialReason,
        CancellationToken cancellationToken)
    {
        await _transactionService.ExecuteInTransactionAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var userExists = await _db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
            if (!userExists)
            {
                _logger.LogDebug("[MetaProgression] Skipping settlement for non-persisted user: GameId={GameId} UserId={UserId}", gameId, userId);
                return;
            }

            var exists = await _db.GameSettlements.AnyAsync(
                s => s.GameId == gameId && s.UserId == userId,
                cancellationToken);

            if (exists)
            {
                _logger.LogDebug("[MetaProgression] Settlement already exists: GameId={GameId} UserId={UserId}", gameId, userId);
                return;
            }

            var isWin = userId == winnerId;
            var result = ResolveGameResult(isWin, reason);
            var rewardCoins = coinRewardEligible
                ? (isWin ? WinCoinReward : LossCoinReward)
                : 0;
            var now = DateTime.UtcNow;
            var durationSeconds = Math.Max(0, (long)(now - gameStartedAtUtc).TotalSeconds);

            _db.GameSettlements.Add(new GameSettlement
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                UserId = userId,
                Result = result,
                WinnerUserId = winnerId,
                SettledAtUtc = DateTime.UtcNow
            });

            // Game session (idempotent per user/game).
            var existingSession = await _db.GameSessions.FirstOrDefaultAsync(
                s => s.UserId == userId && s.GameStateId == gameId,
                cancellationToken);

            if (existingSession == null)
            {
                _db.GameSessions.Add(new GameSession
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    GameStateId = gameId,
                    StartedAt = gameStartedAtUtc,
                    EndedAt = now,
                    Result = result,
                    Duration = durationSeconds,
                    IsActive = false
                });
            }

            // Profile (create if missing).
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
            if (profile == null)
            {
                profile = new UserProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.UserProfiles.Add(profile);
            }
            profile.UpdateGameStats(isWin, gameDuration: durationSeconds);

            // Rank + coin rewards (uses the same transaction when already in one).
            await _economyService.UpdateUserRankAsync(userId, isWin);
            if (rewardCoins > 0)
            {
                await _economyService.AddCoinsAsync(
                    userId,
                    rewardCoins,
                    isWin ? CoinTransactionType.GameWin : CoinTransactionType.GameLoss,
                    description: isWin ? $"对局胜利奖励: {gameId}" : $"对局参与奖励: {gameId}",
                    relatedEntityId: gameId);
            }
            else
            {
                _logger.LogInformation(
                    "[MetaProgression] Coin reward skipped for non-official match. GameId={GameId} UserId={UserId} Reason={Reason}",
                    gameId,
                    userId,
                    nonOfficialReason ?? "Unknown");
            }

            await _questProgressService.RecordActivityAsync(
                userId,
                QuestActivityType.PlayMatch,
                "Match",
                gameId,
                occurredAtUtc: now,
                cancellationToken: cancellationToken);
            if (isWin)
            {
                await _questProgressService.RecordActivityAsync(
                    userId,
                    QuestActivityType.WinMatch,
                    "Match",
                    gameId,
                    occurredAtUtc: now,
                    cancellationToken: cancellationToken);
            }

            _logger.LogInformation(
                "[MetaProgression] Settled GameId={GameId} UserId={UserId} Result={Result} Coins={Coins} CoinRewardEligible={CoinRewardEligible}",
                gameId, userId, result, rewardCoins, coinRewardEligible);
        });
    }

    private static GameResult ResolveGameResult(bool isWin, string? reason)
    {
        if (isWin)
        {
            return GameResult.Win;
        }

        if (IsSurrenderReason(reason))
        {
            return GameResult.Surrender;
        }

        if (IsDisconnectReason(reason))
        {
            return GameResult.Disconnect;
        }

        return GameResult.Loss;
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

    private static bool IsDisconnectReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        return reason.Contains("Disconnect", StringComparison.OrdinalIgnoreCase)
               || reason.Contains("网络连接断开", StringComparison.Ordinal)
               || reason.Contains("断线", StringComparison.Ordinal);
    }
}
