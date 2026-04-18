using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Controllers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public sealed class CardCommunityService : ICardCommunityService
{
    private const int FirstVoteRewardCoins = 10;
    private const int FirstCommentRewardCoins = 50;
    private const string FirstVoteRewardPrefix = "card-community:first-vote:";
    private const string FirstCommentRewardPrefix = "card-community:first-comment:";

    private readonly ApplicationDbContext _db;
    private readonly ITransactionService _transactionService;
    private readonly IEconomyService _economyService;
    private readonly ILogger<CardCommunityService> _logger;

    public CardCommunityService(
        ApplicationDbContext db,
        ITransactionService transactionService,
        IEconomyService economyService,
        ILogger<CardCommunityService> logger)
    {
        _db = db;
        _transactionService = transactionService;
        _economyService = economyService;
        _logger = logger;
    }

    public async Task<CardCommunityController.CardCommentDto> CreateCommentAsync(
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

        RewardGrantResult reward = RewardGrantResult.None;
        await _transactionService.ExecuteInTransactionAsync(async () =>
        {
            _db.CardComments.Add(entity);
            reward = await TryGrantRewardAsync(
                user.Id,
                cardId,
                CoinTransactionType.CardCommunityFirstComment,
                FirstCommentRewardCoins,
                BuildFirstCommentRewardEntityId(cardId),
                "卡牌社区首次评论奖励",
                cancellationToken);
        });

        return new CardCommunityController.CardCommentDto
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
            RewardGranted = reward.Granted,
            RewardCoins = reward.Coins,
            CurrentCoins = await GetCurrentCoinsAsync(user.Id, cancellationToken)
        };
    }

    public async Task<CardCommunityController.CardVoteSummaryDto> VoteAsync(
        int cardId,
        User user,
        CardVoteType voteType,
        CancellationToken cancellationToken = default)
    {
        RewardGrantResult reward = RewardGrantResult.None;
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

                reward = await TryGrantRewardAsync(
                    user.Id,
                    cardId,
                    CoinTransactionType.CardCommunityFirstVote,
                    FirstVoteRewardCoins,
                    BuildFirstVoteRewardEntityId(cardId),
                    "卡牌社区首次投票奖励",
                    cancellationToken);
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

        var voteStats = await BuildVoteStatsAsync(cardId, user.Id, cancellationToken);
        return new CardCommunityController.CardVoteSummaryDto
        {
            CardDefinitionId = cardId,
            UpvoteCount = voteStats.UpvoteCount,
            DownvoteCount = voteStats.DownvoteCount,
            UserVote = voteStats.UserVote,
            RewardGranted = reward.Granted,
            RewardCoins = reward.Coins,
            CurrentCoins = await GetCurrentCoinsAsync(user.Id, cancellationToken)
        };
    }

    private async Task<RewardGrantResult> TryGrantRewardAsync(
        Guid userId,
        int cardId,
        CoinTransactionType rewardType,
        int rewardCoins,
        string relatedEntityId,
        string description,
        CancellationToken cancellationToken)
    {
        if (await HasRewardGrantAsync(userId, rewardType, relatedEntityId, cancellationToken))
        {
            return RewardGrantResult.None;
        }

        var granted = await _economyService.AddCoinsAsync(
            userId,
            rewardCoins,
            rewardType,
            description,
            relatedEntityId);

        if (!granted)
        {
            if (await HasRewardGrantAsync(userId, rewardType, relatedEntityId, cancellationToken))
            {
                _logger.LogInformation(
                    "Card community reward already granted concurrently. UserId={UserId} CardId={CardId} RewardType={RewardType}",
                    userId,
                    cardId,
                    rewardType);
                return RewardGrantResult.None;
            }

            throw new InvalidOperationException(
                $"Failed to persist reward {rewardType} for user {userId} on card {cardId}.");
        }

        return new RewardGrantResult(true, rewardCoins);
    }

    private Task<bool> HasRewardGrantAsync(
        Guid userId,
        CoinTransactionType rewardType,
        string relatedEntityId,
        CancellationToken cancellationToken)
    {
        return _db.CoinTransactions
            .AsNoTracking()
            .AnyAsync(
                transaction => transaction.UserId == userId
                    && transaction.Type == rewardType
                    && transaction.RelatedEntityId == relatedEntityId,
                cancellationToken);
    }

    private async Task<int> GetCurrentCoinsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _db.UserEconomies
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.Coins)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<(int UpvoteCount, int DownvoteCount, string? UserVote)> BuildVoteStatsAsync(
        int cardId,
        Guid userId,
        CancellationToken cancellationToken)
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

        return (upvoteCount, downvoteCount, ToVoteLabel(userVoteType));
    }

    private static string? ToVoteLabel(CardVoteType? voteType)
    {
        return voteType switch
        {
            CardVoteType.Upvote => "up",
            CardVoteType.Downvote => "down",
            _ => null
        };
    }

    private static string BuildFirstVoteRewardEntityId(int cardId) => $"{FirstVoteRewardPrefix}{cardId}";

    private static string BuildFirstCommentRewardEntityId(int cardId) => $"{FirstCommentRewardPrefix}{cardId}";

    private readonly record struct RewardGrantResult(bool Granted, int Coins)
    {
        public static RewardGrantResult None => new(false, 0);
    }
}
