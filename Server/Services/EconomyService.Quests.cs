using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Services;

public partial class EconomyService
{
    #region 任务系统

    public async Task<List<UserQuestDto>> GetUserQuestsAsync(Guid userId)
    {
        if (!await EnsurePersistedUserAsync(userId, nameof(GetUserQuestsAsync)))
        {
            return new List<UserQuestDto>();
        }

        var userQuests = await _context.UserQuests
            .Include(uq => uq.QuestDefinition)
            .Where(uq => uq.UserId == userId)
            .OrderByDescending(uq => uq.IsCompleted)
            .ThenBy(uq => uq.QuestDefinition.Type)
            .ToListAsync();

        return userQuests.Select(UserQuestDto.FromEntity).ToList();
    }

    public async Task<bool> UpdateQuestProgressAsync(Guid userId, QuestType type, int increment = 1)
    {
        if (!await EnsurePersistedUserAsync(userId, nameof(UpdateQuestProgressAsync)))
        {
            return false;
        }

        try
        {
            return await _transactionService.ExecuteInTransactionAsync(async () =>
            {
                var questDefinitions = await _context.QuestDefinitions
                    .Where(qd => qd.Type == type && qd.IsActive)
                    .ToListAsync();

                bool anyCompleted = false;
                var now = DateTime.UtcNow;

                foreach (var definition in questDefinitions)
                {
                    var userQuest = await QuestProgressState.GetOrCreateAsync(
                        _context, userId, definition, now);

                    bool completed = userQuest.UpdateProgress(increment);

                    if (completed)
                    {
                        anyCompleted = true;
                        _logger.LogInformation("用户完成任务(待领取奖励): UserId={UserId}, QuestId={QuestId}, Reward={Reward}",
                            userId, definition.Id, definition.CoinReward);
                    }
                }

                return anyCompleted;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新任务进度失败: UserId={UserId}, Type={Type}", userId, type);
            throw;
        }
    }

    public async Task<QuestRewardClaimDto?> ClaimQuestRewardAsync(Guid userId, Guid userQuestId)
    {
        if (!await EnsurePersistedUserAsync(userId, nameof(ClaimQuestRewardAsync)))
        {
            return null;
        }

        try
        {
            return await _transactionService.ExecuteInTransactionAsync(async () =>
            {
                var userQuest = await _context.UserQuests
                    .Include(uq => uq.QuestDefinition)
                    .FirstOrDefaultAsync(uq => uq.Id == userQuestId && uq.UserId == userId);

                if (userQuest == null)
                {
                    _logger.LogWarning("用户任务不存在: UserId={UserId}, UserQuestId={UserQuestId}", userId, userQuestId);
                    return null;
                }

                if (!userQuest.IsCompleted || userQuest.IsExpired())
                {
                    _logger.LogWarning("用户任务未完成或已过期，无法领取: UserId={UserId}, UserQuestId={UserQuestId}", userId, userQuestId);
                    return null;
                }

                if (!userQuest.ClaimReward())
                {
                    _logger.LogWarning("用户任务奖励已领取: UserId={UserId}, UserQuestId={UserQuestId}", userId, userQuestId);
                    return null;
                }

                var coinReward = userQuest.QuestDefinition.CoinReward;

                var success = await AddCoinsAsync(
                    userId,
                    coinReward,
                    CoinTransactionType.QuestCompletion,
                    $"领取任务奖励: {userQuest.QuestDefinition.Name}",
                    relatedEntityId: userQuest.Id.ToString());

                if (!success)
                {
                    _logger.LogWarning("领取任务奖励发放金币失败: UserId={UserId}, UserQuestId={UserQuestId}", userId, userQuestId);
                    throw new InvalidOperationException(
                        $"Failed to grant quest reward coins for user quest {userQuestId}.");
                }

                _logger.LogInformation("用户领取任务奖励: UserId={UserId}, UserQuestId={UserQuestId}, Reward={Reward}",
                    userId, userQuestId, coinReward);

                var currentCoins = (await GetOrCreateUserEconomyAsync(userId)).Coins;

                return new QuestRewardClaimDto
                {
                    UserId = userId,
                    UserQuestId = userQuest.Id,
                    QuestDefinitionId = userQuest.QuestDefinitionId,
                    CoinReward = coinReward,
                    CurrentCoins = currentCoins,
                    ClaimedAtUtc = userQuest.RewardClaimedAt!.Value
                };
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "领取任务奖励失败: UserId={UserId}, UserQuestId={UserQuestId}", userId, userQuestId);
            throw;
        }
    }

    public async Task<List<UserQuestDto>> CheckAndCompleteQuestsAsync(Guid userId)
    {
        if (!await EnsurePersistedUserAsync(userId, nameof(CheckAndCompleteQuestsAsync)))
        {
            return new List<UserQuestDto>();
        }

        try
        {
            return await _transactionService.ExecuteInTransactionAsync(async () =>
            {
                await UpdateQuestProgressAsync(userId, QuestType.WinGames, 0);
                await UpdateQuestProgressAsync(userId, QuestType.PlayGames, 0);

                var recentlyCompleted = await _context.UserQuests
                    .Include(uq => uq.QuestDefinition)
                    .Where(uq => uq.UserId == userId && uq.IsCompleted &&
                                uq.CompletedAt.HasValue && uq.CompletedAt.Value >= DateTime.UtcNow.AddMinutes(-5))
                    .ToListAsync();

                return recentlyCompleted.Select(UserQuestDto.FromEntity).ToList();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查完成任务失败: UserId={UserId}", userId);
            throw;
        }
    }

    #endregion
}
