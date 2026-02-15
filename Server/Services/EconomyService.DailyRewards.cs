using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Services;

public partial class EconomyService
{
    #region 每日奖励

    public async Task<DailyRewardDto?> ClaimDailyRewardAsync(Guid userId)
    {
        if (!await EnsurePersistedUserAsync(userId, nameof(ClaimDailyRewardAsync)))
        {
            return null;
        }

        try
        {
            return await _transactionService.ExecuteInTransactionAsync(async () =>
            {
                var userDailyReward = await GetOrCreateUserDailyRewardAsync(userId);

                if (!userDailyReward.CanClaimDailyReward())
                {
                    _logger.LogWarning("用户今日已领取每日奖励: UserId={UserId}", userId);
                    return null;
                }

                var totalReward = userDailyReward.ClaimDailyReward();

                await AddCoinsAsync(
                    userId,
                    totalReward,
                    CoinTransactionType.DailyLoginBonus,
                    $"每日登录奖励 (连续{userDailyReward.ConsecutiveDays}天)",
                    relatedEntityId: userDailyReward.LastClaimedAt.Date.ToString("yyyy-MM-dd"));

                _logger.LogInformation("用户领取每日奖励: UserId={UserId}, Reward={Reward}, Streak={Streak}",
                    userId, totalReward, userDailyReward.ConsecutiveDays);

                return DailyRewardDto.Create(userId, totalReward, userDailyReward.ConsecutiveDays);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "领取每日奖励失败: UserId={UserId}", userId);
            return null;
        }
    }

    public async Task<bool> CanClaimDailyRewardAsync(Guid userId)
    {
        if (!await EnsurePersistedUserAsync(userId, nameof(CanClaimDailyRewardAsync)))
        {
            return false;
        }

        var userDailyReward = await GetOrCreateUserDailyRewardAsync(userId);
        return userDailyReward.CanClaimDailyReward();
    }

    #endregion
}
