using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Services;

public partial class EconomyService
{
    #region 排名系统

    public async Task<UserRankDto?> GetUserRankAsync(Guid userId)
    {
        if (!await EnsurePersistedUserAsync(userId, nameof(GetUserRankAsync)))
        {
            return null;
        }

        var userEconomy = await GetOrCreateUserEconomyAsync(userId);
        var userRanking = await GetOrCreateUserRankingAsync(userId);
        var userDailyReward = await GetOrCreateUserDailyRewardAsync(userId);
        return UserRankDto.FromEntities(userEconomy, userRanking, userDailyReward);
    }

    public async Task<MetaProgressionStateDto> GetMetaProgressionStateAsync(Guid userId)
    {
        if (!await EnsurePersistedUserAsync(userId, nameof(GetMetaProgressionStateAsync)))
        {
            return new MetaProgressionStateDto
            {
                UserId = userId,
                Rank = null,
                Quests = new List<UserQuestDto>()
            };
        }

        var rank = await GetUserRankAsync(userId);
        var quests = await GetUserQuestsAsync(userId);

        return new MetaProgressionStateDto
        {
            UserId = userId,
            Rank = rank,
            Quests = quests
        };
    }

    public async Task<bool> UpdateUserRankAsync(Guid userId, bool isWin)
    {
        if (!await EnsurePersistedUserAsync(userId, nameof(UpdateUserRankAsync)))
        {
            return false;
        }

        try
        {
            return await _transactionService.ExecuteInTransactionAsync(async () =>
            {
                var userRanking = await GetOrCreateUserRankingAsync(userId);

                userRanking.ProcessGameResult(isWin);

                _logger.LogInformation("更新用户排名: UserId={UserId}, IsWin={IsWin}, NewRank={Rank}, NewPoints={Points}",
                    userId, isWin, userRanking.Rank, userRanking.RankPoints);

                return true;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新用户排名失败: UserId={UserId}", userId);
            return false;
        }
    }

    public async Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(int page = 1, int pageSize = 50)
    {
        var skip = (page - 1) * pageSize;

        var leaderboardData = await _context.UserRankings
            .Include(ur => ur.User)
            .Include(ur => ur.User.Profile)
            .Where(ur => ur.User.IsActive)
            .OrderByDescending(ur => ur.Rank)
            .ThenByDescending(ur => ur.RankPoints)
            .ThenByDescending(ur => ur.User.Profile!.WinRate)
            .Skip(skip)
            .Take(pageSize)
            .Select((ur, index) => new LeaderboardEntryDto
            {
                Position = skip + index + 1,
                UserId = ur.UserId,
                Username = ur.User.Username,
                AvatarUrl = ur.User.AvatarUrl,
                Rank = ur.Rank,
                RankName = ur.GetRankName(),
                RankPoints = ur.RankPoints,
                TotalGames = ur.User.Profile!.TotalGames,
                Wins = ur.User.Profile!.Wins,
                WinRate = ur.User.Profile!.WinRate,
                TotalPlayTimeHours = Math.Round((decimal)ur.User.Profile!.TotalPlayTime / 3600, 1)
            })
            .ToListAsync();

        return leaderboardData;
    }

    #endregion
}
