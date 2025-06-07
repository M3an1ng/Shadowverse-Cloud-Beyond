//
// 相关文档:
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 经济系统: 文档/05-游戏系统/06-经济系统.md
// - API文档: 文档/06-API文档/01-端点总览.md
//
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Services;

/// <summary>
/// 经济系统服务实现
/// </summary>
public class EconomyService : IEconomyService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EconomyService> _logger;

    public EconomyService(ApplicationDbContext context, ILogger<EconomyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region 金币管理

    public async Task<int> GetUserCoinsAsync(Guid userId)
    {
        var userEconomy = await GetOrCreateUserEconomyAsync(userId);
        return userEconomy.Coins;
    }

    public async Task<bool> AddCoinsAsync(Guid userId, int amount, CoinTransactionType type, string? description = null, string? relatedEntityId = null)
    {
        if (amount <= 0)
        {
            _logger.LogWarning("尝试添加非正数金币: UserId={UserId}, Amount={Amount}", userId, amount);
            return false;
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 获取用户经济信息
            var userEconomy = await GetOrCreateUserEconomyAsync(userId);

            // 更新金币
            userEconomy.Coins += amount;
            userEconomy.UpdatedAt = DateTime.UtcNow;

            // 记录交易
            var coinTransaction = new CoinTransaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = amount,
                Type = type,
                Description = description,
                RelatedEntityId = relatedEntityId,
                CreatedAt = DateTime.UtcNow
            };

            _context.CoinTransactions.Add(coinTransaction);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("成功添加金币: UserId={UserId}, Amount={Amount}, Type={Type}", userId, amount, type);
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "添加金币失败: UserId={UserId}, Amount={Amount}", userId, amount);
            return false;
        }
    }

    public async Task<bool> SpendCoinsAsync(Guid userId, int amount, CoinTransactionType type, string? description = null, string? relatedEntityId = null)
    {
        if (amount <= 0)
        {
            _logger.LogWarning("尝试扣除非正数金币: UserId={UserId}, Amount={Amount}", userId, amount);
            return false;
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 获取用户经济信息
            var userEconomy = await GetOrCreateUserEconomyAsync(userId);

            // 检查金币是否足够
            if (userEconomy.Coins < amount)
            {
                _logger.LogWarning("金币不足: UserId={UserId}, Required={Amount}, Available={Available}", userId, amount, userEconomy.Coins);
                return false;
            }

            // 扣除金币
            userEconomy.Coins -= amount;
            userEconomy.UpdatedAt = DateTime.UtcNow;

            // 记录交易 (负数表示支出)
            var coinTransaction = new CoinTransaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = -amount,
                Type = type,
                Description = description,
                RelatedEntityId = relatedEntityId,
                CreatedAt = DateTime.UtcNow
            };

            _context.CoinTransactions.Add(coinTransaction);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("成功扣除金币: UserId={UserId}, Amount={Amount}, Type={Type}", userId, amount, type);
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "扣除金币失败: UserId={UserId}, Amount={Amount}", userId, amount);
            return false;
        }
    }

    public async Task<List<CoinTransactionDto>> GetCoinHistoryAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        var skip = (page - 1) * pageSize;

        var transactions = await _context.CoinTransactions
            .Where(ct => ct.UserId == userId)
            .OrderByDescending(ct => ct.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return transactions.Select(CoinTransactionDto.FromEntity).ToList();
    }

    #endregion

    #region 卡包系统

    public async Task<List<CardPackDefinitionDto>> GetAvailableCardPacksAsync()
    {
        var cardPacks = await _context.CardPackDefinitions
            .Where(cpd => cpd.IsActive)
            .OrderBy(cpd => cpd.Price)
            .ToListAsync();

        return cardPacks.Select(CardPackDefinitionDto.FromEntity).ToList();
    }

    public async Task<UserCardPackDto?> PurchaseCardPackAsync(Guid userId, int cardPackDefinitionId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 获取卡包定义
            var cardPackDefinition = await _context.CardPackDefinitions
                .FirstOrDefaultAsync(cpd => cpd.Id == cardPackDefinitionId && cpd.IsActive);

            if (cardPackDefinition == null)
            {
                _logger.LogWarning("卡包定义不存在或已停用: CardPackDefinitionId={CardPackDefinitionId}", cardPackDefinitionId);
                return null;
            }

            // 扣除金币
            var success = await SpendCoinsAsync(userId, cardPackDefinition.Price, CoinTransactionType.CardPackPurchase,
                $"购买{cardPackDefinition.Name}", cardPackDefinitionId.ToString());

            if (!success)
            {
                return null;
            }

            // 创建用户卡包
            var userCardPack = new UserCardPack
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CardPackDefinitionId = cardPackDefinitionId,
                PurchasedAt = DateTime.UtcNow
            };

            _context.UserCardPacks.Add(userCardPack);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // 加载关联数据并返回DTO
            userCardPack.CardPackDefinition = cardPackDefinition;

            _logger.LogInformation("成功购买卡包: UserId={UserId}, CardPackId={CardPackId}, Price={Price}",
                userId, userCardPack.Id, cardPackDefinition.Price);

            return UserCardPackDto.FromEntity(userCardPack);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "购买卡包失败: UserId={UserId}, CardPackDefinitionId={CardPackDefinitionId}",
                userId, cardPackDefinitionId);
            return null;
        }
    }

    public async Task<List<UserCardDto>> OpenCardPackAsync(Guid userId, Guid cardPackId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 获取用户卡包
            var userCardPack = await _context.UserCardPacks
                .Include(ucp => ucp.CardPackDefinition)
                .FirstOrDefaultAsync(ucp => ucp.Id == cardPackId && ucp.UserId == userId);

            if (userCardPack == null)
            {
                _logger.LogWarning("用户卡包不存在: UserId={UserId}, CardPackId={CardPackId}", userId, cardPackId);
                return new List<UserCardDto>();
            }

            if (userCardPack.IsOpened)
            {
                _logger.LogWarning("卡包已经开启过: UserId={UserId}, CardPackId={CardPackId}", userId, cardPackId);
                return new List<UserCardDto>();
            }

            // 生成卡片 (这里需要实现卡片生成逻辑)
            var generatedCards = await GenerateCardsForPackAsync(userCardPack);

            // 开启卡包
            userCardPack.Open(generatedCards);

            // 保存到数据库
            _context.UserCards.AddRange(generatedCards);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("成功开启卡包: UserId={UserId}, CardPackId={CardPackId}, CardsCount={CardsCount}",
                userId, cardPackId, generatedCards.Count);

            return generatedCards.Select(UserCardDto.FromEntity).ToList();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "开启卡包失败: UserId={UserId}, CardPackId={CardPackId}", userId, cardPackId);
            return new List<UserCardDto>();
        }
    }

    public async Task<List<UserCardPackDto>> GetUserCardPacksAsync(Guid userId, bool unopenedOnly = false)
    {
        var query = _context.UserCardPacks
            .Include(ucp => ucp.CardPackDefinition)
            .Where(ucp => ucp.UserId == userId);

        if (unopenedOnly)
        {
            query = query.Where(ucp => !ucp.IsOpened);
        }

        var userCardPacks = await query
            .OrderByDescending(ucp => ucp.PurchasedAt)
            .ToListAsync();

        return userCardPacks.Select(ucp => UserCardPackDto.FromEntity(ucp)).ToList();
    }

    #endregion

    #region 用户经济信息

    public async Task<UserEconomy> GetOrCreateUserEconomyAsync(Guid userId)
    {
        var userEconomy = await _context.UserEconomies
            .FirstOrDefaultAsync(ue => ue.UserId == userId);

        if (userEconomy == null)
        {
            userEconomy = new UserEconomy
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Coins = 1000, // 初始金币

                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserEconomies.Add(userEconomy);
            await _context.SaveChangesAsync();

            _logger.LogInformation("创建新用户经济信息: UserId={UserId}", userId);
        }

        return userEconomy;
    }

    public async Task<bool> InitializeUserEconomyAsync(Guid userId)
    {
        try
        {
            await GetOrCreateUserEconomyAsync(userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化用户经济信息失败: UserId={UserId}", userId);
            return false;
        }
    }

    #endregion

    #region 排名系统

    public async Task<UserRankDto?> GetUserRankAsync(Guid userId)
    {
        var userEconomy = await GetOrCreateUserEconomyAsync(userId);
        return UserRankDto.FromEntity(userEconomy);
    }

    public async Task<bool> UpdateUserRankAsync(Guid userId, bool isWin)
    {
        try
        {
            // 获取或创建用户排名记录
            var userRanking = await GetOrCreateUserRankingAsync(userId);

            userRanking.ProcessGameResult(isWin);

            await _context.SaveChangesAsync();

            _logger.LogInformation("更新用户排名: UserId={UserId}, IsWin={IsWin}, NewRank={Rank}, NewPoints={Points}",
                userId, isWin, userRanking.Rank, userRanking.RankPoints);

            return true;
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

    #region 任务系统

    public async Task<List<UserQuestDto>> GetUserQuestsAsync(Guid userId)
    {
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
        try
        {
            // 获取该类型的所有任务定义
            var questDefinitions = await _context.QuestDefinitions
                .Where(qd => qd.Type == type && qd.IsActive)
                .ToListAsync();

            bool anyCompleted = false;

            foreach (var definition in questDefinitions)
            {
                // 获取或创建用户任务
                var userQuest = await _context.UserQuests
                    .Include(uq => uq.QuestDefinition)
                    .FirstOrDefaultAsync(uq => uq.UserId == userId && uq.QuestDefinitionId == definition.Id);

                if (userQuest == null)
                {
                    userQuest = new UserQuest
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        QuestDefinitionId = definition.Id,
                        CurrentProgress = 0,
                        IsCompleted = false,
                        IsRewardClaimed = false,
                        AssignedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddHours(definition.RefreshIntervalHours)
                    };
                    userQuest.QuestDefinition = definition;
                    _context.UserQuests.Add(userQuest);
                }

                // 更新进度
                bool completed = userQuest.UpdateProgress(increment);

                if (completed)
                {
                    anyCompleted = true;

                    // 发放任务奖励
                    await AddCoinsAsync(userId, definition.CoinReward, CoinTransactionType.QuestCompletion,
                        $"完成任务: {definition.Name}", definition.Id.ToString());

                    _logger.LogInformation("用户完成任务: UserId={UserId}, QuestId={QuestId}, Reward={Reward}",
                        userId, definition.Id, definition.CoinReward);
                }
            }

            await _context.SaveChangesAsync();
            return anyCompleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新任务进度失败: UserId={UserId}, Type={Type}", userId, type);
            return false;
        }
    }

    public async Task<List<UserQuestDto>> CheckAndCompleteQuestsAsync(Guid userId)
    {
        var completedQuests = new List<UserQuestDto>();

        try
        {
            // 检查各种类型的任务
            await UpdateQuestProgressAsync(userId, QuestType.WinGames, 0);
            await UpdateQuestProgressAsync(userId, QuestType.PlayGames, 0);

            // 获取刚完成的任务
            var recentlyCompleted = await _context.UserQuests
                .Include(uq => uq.QuestDefinition)
                .Where(uq => uq.UserId == userId && uq.IsCompleted &&
                            uq.CompletedAt.HasValue && uq.CompletedAt.Value >= DateTime.UtcNow.AddMinutes(-5))
                .ToListAsync();

            completedQuests = recentlyCompleted.Select(UserQuestDto.FromEntity).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查完成任务失败: UserId={UserId}", userId);
        }

        return completedQuests;
    }

    #endregion

    #region 每日奖励

    public async Task<DailyRewardDto?> ClaimDailyRewardAsync(Guid userId)
    {
        try
        {
            // 获取或创建用户每日奖励记录
            var userDailyReward = await GetOrCreateUserDailyRewardAsync(userId);

            if (!userDailyReward.CanClaimDailyReward())
            {
                _logger.LogWarning("用户今日已领取每日奖励: UserId={UserId}", userId);
                return null;
            }

            // 领取奖励并获得金币数量
            var totalReward = userDailyReward.ClaimDailyReward();

            // 发放奖励
            await AddCoinsAsync(userId, totalReward, CoinTransactionType.DailyLoginBonus,
                $"每日登录奖励 (连续{userDailyReward.ConsecutiveDays}天)");

            await _context.SaveChangesAsync();

            _logger.LogInformation("用户领取每日奖励: UserId={UserId}, Reward={Reward}, Streak={Streak}",
                userId, totalReward, userDailyReward.ConsecutiveDays);

            return DailyRewardDto.Create(userId, totalReward, userDailyReward.ConsecutiveDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "领取每日奖励失败: UserId={UserId}", userId);
            return null;
        }
    }

    public async Task<bool> CanClaimDailyRewardAsync(Guid userId)
    {
        var userDailyReward = await GetOrCreateUserDailyRewardAsync(userId);
        return userDailyReward.CanClaimDailyReward();
    }

    #endregion

    #region 私有辅助方法

    /// <summary>
    /// 获取或创建用户排名记录
    /// 遵循单一职责原则，专门处理排名相关逻辑
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>用户排名实体</returns>
    private async Task<UserRanking> GetOrCreateUserRankingAsync(Guid userId)
    {
        var userRanking = await _context.UserRankings
            .FirstOrDefaultAsync(ur => ur.UserId == userId);

        if (userRanking == null)
        {
            userRanking = new UserRanking
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Rank = 0,
                RankPoints = 0,
                SeasonStartAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.UserRankings.Add(userRanking);
            await _context.SaveChangesAsync();
        }

        return userRanking;
    }

    /// <summary>
    /// 获取或创建用户每日奖励记录
    /// 遵循单一职责原则，专门处理每日奖励相关逻辑
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>用户每日奖励实体</returns>
    private async Task<UserDailyReward> GetOrCreateUserDailyRewardAsync(Guid userId)
    {
        var userDailyReward = await _context.UserDailyRewards
            .FirstOrDefaultAsync(udr => udr.UserId == userId);

        if (userDailyReward == null)
        {
            userDailyReward = new UserDailyReward
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LastClaimedAt = DateTime.MinValue,
                ConsecutiveDays = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.UserDailyRewards.Add(userDailyReward);
            await _context.SaveChangesAsync();
        }

        return userDailyReward;
    }

    /// <summary>
    /// 为卡包生成卡片
    /// </summary>
    /// <param name="userCardPack">用户卡包</param>
    /// <returns>生成的卡片列表</returns>
    private async Task<List<UserCard>> GenerateCardsForPackAsync(UserCardPack userCardPack)
    {
        // TODO: 实现真正的卡片生成逻辑
        // 这里先返回一个简单的示例实现
        var cards = new List<UserCard>();
        var random = new Random();

        // 获取所有可收集的卡片
        var availableCards = await _context.CardDefinitions
            .Where(cd => cd.IsCollectible)
            .ToListAsync();

        if (!availableCards.Any())
        {
            _logger.LogWarning("没有可收集的卡片定义");
            return cards;
        }

        // 生成指定数量的卡片
        for (int i = 0; i < userCardPack.CardPackDefinition!.CardCount; i++)
        {
            var randomCard = availableCards[random.Next(availableCards.Count)];

            var userCard = new UserCard
            {
                Id = Guid.NewGuid(),
                UserId = userCardPack.UserId,
                CardPackId = userCardPack.Id,
                CardDefinitionId = randomCard.Id,
                Count = 1,
                ObtainedAt = DateTime.UtcNow
            };

            cards.Add(userCard);
        }

        return cards;
    }

    #endregion
}
