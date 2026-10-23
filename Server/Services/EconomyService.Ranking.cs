using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

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

    public async Task<MetaProgressionStateDto> GetMetaProgressionStateAsync(Guid userId, string? environment = null, string? deckRule = null)
    {
        if (!await EnsurePersistedUserAsync(userId, nameof(GetMetaProgressionStateAsync)))
        {
            return new MetaProgressionStateDto
            {
                UserId = userId,
                Rank = null,
                Quests = new List<UserQuestDto>(),
                ClaimableQuestCount = 0,
                ClassStats = new List<UserClassWinStatsDto>()
            };
        }

        var rank = await GetUserRankAsync(userId);
        var quests = await GetUserQuestsAsync(userId);
        var environmentCode = ResolveWinStatsEnvironmentCode(environment);
        var deckRuleCode = ResolveWinStatsDeckRuleCode(deckRule);
        var participantFactsQuery = _context.GameParticipantStatsFacts
            .AsNoTracking()
            .Where(item => item.PlayerId == userId && item.IsOfficial);
        if (environmentCode.HasValue)
        {
            participantFactsQuery = participantFactsQuery.Where(item => (int)item.Environment == environmentCode.Value);
        }
        if (deckRuleCode.HasValue)
        {
            participantFactsQuery = participantFactsQuery.Where(item => (int)item.Rule == deckRuleCode.Value);
        }

        var classStats = await participantFactsQuery
            .GroupBy(item => item.DeckClass)
            .OrderBy(group => group.Key)
            .Select(group => new UserClassWinStatsDto
            {
                DeckClass = group.Key.ToString(),
                TotalGames = group.Count(),
                TotalWins = group.Sum(item => item.IsWinner ? 1 : 0)
            })
            .ToListAsync();

        return new MetaProgressionStateDto
        {
            UserId = userId,
            Rank = rank,
            Quests = quests,
            ClaimableQuestCount = quests.Count(quest => quest.IsCompleted && !quest.IsRewardClaimed),
            ClassStats = classStats
        };
    }

    private static int? ResolveWinStatsEnvironmentCode(string? environment)
    {
        var key = string.IsNullOrWhiteSpace(environment) ? nameof(DeckEnvironment.Rotation) : environment.Trim();
        if (string.Equals(key, "all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Enum.TryParse<DeckEnvironment>(key, true, out var parsedEnvironment)
            && Enum.IsDefined(typeof(DeckEnvironment), parsedEnvironment))
        {
            return (int)parsedEnvironment;
        }

        if (int.TryParse(key, out var parsedValue)
            && Enum.IsDefined(typeof(DeckEnvironment), parsedValue))
        {
            return parsedValue;
        }

        return (int)DeckEnvironment.Rotation;
    }

    private static int? ResolveWinStatsDeckRuleCode(string? deckRule)
    {
        if (string.IsNullOrWhiteSpace(deckRule))
        {
            return null;
        }

        var key = deckRule.Trim();
        if (string.Equals(key, "all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Enum.TryParse<DeckRule>(key, true, out var parsedRule)
            && Enum.IsDefined(typeof(DeckRule), parsedRule))
        {
            return (int)parsedRule;
        }

        if (int.TryParse(key, out var parsedValue)
            && Enum.IsDefined(typeof(DeckRule), parsedValue))
        {
            return parsedValue;
        }

        return null;
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



