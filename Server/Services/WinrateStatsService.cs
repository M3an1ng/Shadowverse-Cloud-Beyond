using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;
using System.Linq;

namespace ShadowverseBeyond.Server.Services;

public sealed class WinrateStatsService : IWinrateStatsService
{
    private readonly ApplicationDbContext _db;
    private readonly ITransactionService _transactionService;
    private readonly IGameStatsRuntimeTracker _runtimeTracker;
    private readonly IOfficialMatchEvaluator _officialMatchEvaluator;
    private readonly ILogger<WinrateStatsService> _logger;

    public WinrateStatsService(
        ApplicationDbContext db,
        ITransactionService transactionService,
        IGameStatsRuntimeTracker runtimeTracker,
        IOfficialMatchEvaluator officialMatchEvaluator,
        ILogger<WinrateStatsService> logger)
    {
        _db = db;
        _transactionService = transactionService;
        _runtimeTracker = runtimeTracker;
        _officialMatchEvaluator = officialMatchEvaluator;
        _logger = logger;
    }

    public async Task ApplyPostCommitAsync(
        GameContext gameContext,
        IReadOnlyList<IGameEvent> processedEvents,
        CancellationToken cancellationToken)
    {
        var gameState = gameContext.CurrentGameState;
        if (gameState == null || string.IsNullOrWhiteSpace(gameState.GameId))
        {
            return;
        }

        _runtimeTracker.TrackCommittedEvents(gameState, processedEvents);

        var gameEndEvent = processedEvents.LastOrDefault(e => e is GameEndEvent) as GameEndEvent;
        if (gameEndEvent == null)
        {
            return;
        }

        try
        {
            await _transactionService.ExecuteInTransactionAsync(async () =>
            {
                var snapshot = _runtimeTracker.GetSnapshot(gameState.GameId);
                if (snapshot == null)
                {
                    _logger.LogWarning("[WinrateStats] Runtime snapshot is missing for GameId={GameId}.", gameState.GameId);
                    return;
                }

                var officialMatch = await _officialMatchEvaluator.EvaluateAsync(gameState, gameEndEvent, cancellationToken);

                foreach (var participant in snapshot.Participants.Values)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var exists = await _db.GameParticipantStatsFacts
                        .AsNoTracking()
                        .AnyAsync(
                            fact => fact.GameId == snapshot.GameId && fact.PlayerId == participant.PlayerId,
                            cancellationToken);

                    if (exists)
                    {
                        continue;
                    }

                    var isWinner = participant.PlayerId == gameEndEvent.WinnerId;
                    _db.GameParticipantStatsFacts.Add(new GameParticipantStatsFact
                    {
                        Id = Guid.NewGuid(),
                        GameId = snapshot.GameId,
                        PlayerId = participant.PlayerId,
                        IsWinner = isWinner,
                        EndedAtUtc = gameEndEvent.Timestamp,
                        DeckClass = participant.DeckClass,
                        Environment = participant.Environment,
                        DeckShapeHash = participant.DeckShapeHash,
                        UserDeckId = participant.UserDeckId,
                        InitialDeckCardCountsJson = JsonSerializer.Serialize(participant.InitialDeckCardCounts),
                        DrawnCardIdsJson = JsonSerializer.Serialize(participant.DrawnCardDefinitionIds.OrderBy(id => id).ToArray()),
                        PlayedCardIdsJson = JsonSerializer.Serialize(participant.PlayedCardDefinitionIds.OrderBy(id => id).ToArray()),
                        IsOfficial = officialMatch.IsOfficial
                    });

                    if (!officialMatch.IsOfficial)
                    {
                        continue;
                    }

                    await UpdateCardAggAsync(participant, isWinner, cancellationToken);
                    await UpdateDeckAggAsync(participant, isWinner, cancellationToken);
                    await UpdateUserClassAggAsync(participant.PlayerId, participant.DeckClass, isWinner, cancellationToken);
                }
            });
        }
        finally
        {
            _runtimeTracker.Clear(gameState.GameId);
        }
    }

    private async Task UpdateCardAggAsync(
        ParticipantRuntimeStatsSnapshot participant,
        bool isWinner,
        CancellationToken cancellationToken)
    {
        foreach (var cardDefinitionId in participant.InitialDeckCardCounts.Keys)
        {
            var cardAgg = _db.CardStatsAggs.Local.FirstOrDefault(
                item => item.Environment == participant.Environment && item.CardDefinitionId == cardDefinitionId);

            if (cardAgg == null)
            {
                cardAgg = await _db.CardStatsAggs.FirstOrDefaultAsync(
                    item => item.Environment == participant.Environment && item.CardDefinitionId == cardDefinitionId,
                    cancellationToken);
            }

            if (cardAgg == null)
            {
                cardAgg = new CardStatsAgg
                {
                    Id = Guid.NewGuid(),
                    Environment = participant.Environment,
                    CardDefinitionId = cardDefinitionId
                };
                _db.CardStatsAggs.Add(cardAgg);
            }

            cardAgg.DeckGames++;
            if (isWinner)
            {
                cardAgg.DeckWins++;
            }

            if (participant.DrawnCardDefinitionIds.Contains(cardDefinitionId))
            {
                cardAgg.DrawnGames++;
                if (isWinner)
                {
                    cardAgg.DrawnWins++;
                }
            }

            if (participant.PlayedCardDefinitionIds.Contains(cardDefinitionId))
            {
                cardAgg.PlayedGames++;
                if (isWinner)
                {
                    cardAgg.PlayedWins++;
                }
            }

            cardAgg.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    private async Task UpdateDeckAggAsync(
        ParticipantRuntimeStatsSnapshot participant,
        bool isWinner,
        CancellationToken cancellationToken)
    {
        var deckAgg = _db.DeckStatsAggs.Local.FirstOrDefault(
            item => item.Environment == participant.Environment && item.DeckShapeHash == participant.DeckShapeHash);

        if (deckAgg == null)
        {
            deckAgg = await _db.DeckStatsAggs.FirstOrDefaultAsync(
                item => item.Environment == participant.Environment && item.DeckShapeHash == participant.DeckShapeHash,
                cancellationToken);
        }

        if (deckAgg == null)
        {
            deckAgg = new DeckStatsAgg
            {
                Id = Guid.NewGuid(),
                Environment = participant.Environment,
                DeckShapeHash = participant.DeckShapeHash,
                DeckClass = participant.DeckClass
            };
            _db.DeckStatsAggs.Add(deckAgg);
        }

        deckAgg.TotalGames++;
        if (isWinner)
        {
            deckAgg.TotalWins++;
        }

        deckAgg.UpdatedAtUtc = DateTime.UtcNow;
    }

    private async Task UpdateUserClassAggAsync(
        Guid userId,
        CardClass deckClass,
        bool isWinner,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        var userClassAgg = _db.UserClassStatsAggs.Local.FirstOrDefault(
            item => item.UserId == userId && item.DeckClass == deckClass);

        if (userClassAgg == null)
        {
            userClassAgg = await _db.UserClassStatsAggs.FirstOrDefaultAsync(
                item => item.UserId == userId && item.DeckClass == deckClass,
                cancellationToken);
        }

        if (userClassAgg == null)
        {
            userClassAgg = new UserClassStatsAgg
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DeckClass = deckClass
            };
            _db.UserClassStatsAggs.Add(userClassAgg);
        }

        userClassAgg.TotalGames++;
        if (isWinner)
        {
            userClassAgg.TotalWins++;
        }

        userClassAgg.UpdatedAtUtc = DateTime.UtcNow;
    }
}
