using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public sealed class CardCommunityService : ICardCommunityService
{
    private readonly ApplicationDbContext _db;
    private readonly ITransactionService _transactionService;
    private readonly IQuestProgressService _questProgressService;

    public CardCommunityService(
        ApplicationDbContext db,
        ITransactionService transactionService,
        IQuestProgressService questProgressService,
        ILogger<CardCommunityService> logger)
    {
        _db = db;
        _transactionService = transactionService;
        _questProgressService = questProgressService;
    }

    public async Task<CardCommentDto> CreateCommentAsync(
        int cardId,
        User user,
        string content,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entity = new CardComment
        {
            Id = Guid.NewGuid(),
            CardDefinitionId = cardId,
            UserId = user.Id,
            Content = content,
            CreatedAt = now,
            UpdatedAt = now
        };

        QuestProgressResult questProgress = new()
        {
            UserId = user.Id,
            ActivityType = QuestActivityType.Comment
        };
        await _transactionService.ExecuteInTransactionAsync(async () =>
        {
            _db.CardComments.Add(entity);
            questProgress = await _questProgressService.RecordActivityAsync(
                user.Id,
                QuestActivityType.Comment,
                "Card",
                cardId.ToString(),
                occurredAtUtc: now,
                cancellationToken: cancellationToken);
        });

        return new CardCommentDto
        {
            Id = entity.Id,
            CardDefinitionId = entity.CardDefinitionId,
            UserId = entity.UserId,
            Username = user.Username,
            AvatarUrl = user.AvatarUrl,
            Content = entity.Content,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CanDelete = true,
            RewardGranted = false,
            RewardCoins = 0,
            CurrentCoins = null,
            QuestProgress = ToQuestProgressDto(questProgress)
        };
    }

    public async Task<CardVoteSummaryDto> VoteAsync(
        int cardId,
        User user,
        CardVoteType voteType,
        CancellationToken cancellationToken = default)
    {
        QuestProgressResult questProgress = new()
        {
            UserId = user.Id,
            ActivityType = QuestActivityType.Vote
        };
        await _transactionService.ExecuteInTransactionAsync(async () =>
        {
            var entity = await _db.CardVotes
                .FirstOrDefaultAsync(vote =>
                    vote.CardDefinitionId == cardId &&
                    vote.UserId == user.Id,
                    cancellationToken);

            if (entity == null)
            {
                _db.CardVotes.Add(new CardVote
                {
                    Id = Guid.NewGuid(),
                    CardDefinitionId = cardId,
                    UserId = user.Id,
                    VoteType = voteType,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

                questProgress = await _questProgressService.RecordActivityAsync(
                    user.Id,
                    QuestActivityType.Vote,
                    "Card",
                    cardId.ToString(),
                    cancellationToken: cancellationToken);
                return;
            }

            if (entity.VoteType == voteType)
            {
                // Same button again cancels the vote.
                _db.CardVotes.Remove(entity);
                return;
            }

            entity.VoteType = voteType;
            entity.UpdatedAt = DateTime.UtcNow;
        });

        var summary = await GetVoteSummaryAsync(cardId, user.Id, cancellationToken);
        summary.QuestProgress = ToQuestProgressDto(questProgress);
        return summary;
    }

    private static QuestProgressDto ToQuestProgressDto(QuestProgressResult result)
    {
        return new QuestProgressDto
        {
            UpdatedQuestIds = result.UpdatedQuestIds,
            CompletedQuestIds = result.CompletedQuestIds,
            ClaimableCount = result.CompletedQuestIds.Count,
            IgnoredAsDuplicate = result.IgnoredAsDuplicate
        };
    }

    public async Task<CardVoteSummaryDto> GetVoteSummaryAsync(
        int cardId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var upvoteCount = await _db.CardVotes.CountAsync(
            vote => vote.CardDefinitionId == cardId && vote.VoteType == CardVoteType.Upvote,
            cancellationToken);
        var downvoteCount = await _db.CardVotes.CountAsync(
            vote => vote.CardDefinitionId == cardId && vote.VoteType == CardVoteType.Downvote,
            cancellationToken);
        var userVoteType = await _db.CardVotes
            .Where(vote => vote.CardDefinitionId == cardId && vote.UserId == userId)
            .Select(vote => (CardVoteType?)vote.VoteType)
            .FirstOrDefaultAsync(cancellationToken);

        return new CardVoteSummaryDto
        {
            CardDefinitionId = cardId,
            UpvoteCount = upvoteCount,
            DownvoteCount = downvoteCount,
            UserVote = userVoteType.HasValue ? CardVoteLabels.ToLabel(userVoteType.Value) : null
        };
    }
}
