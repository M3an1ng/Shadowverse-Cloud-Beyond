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
public partial class EconomyService : IEconomyService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EconomyService> _logger;
    private readonly ITransactionService _transactionService;

    public EconomyService(ApplicationDbContext context, ILogger<EconomyService> logger, ITransactionService transactionService)
    {
        _context = context;
        _logger = logger;
        _transactionService = transactionService;
    }

    #region 私有辅助方法

    private async Task<bool> UserExistsAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return false;
        }

        return await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId);
    }

    private async Task<bool> EnsurePersistedUserAsync(Guid userId, string operation)
    {
        if (await UserExistsAsync(userId))
        {
            return true;
        }

        _logger.LogWarning("[Economy] Skip {Operation}: user does not exist. UserId={UserId}", operation, userId);
        return false;
    }

    /// <summary>
    /// 获取或创建用户排名记录
    /// 遵循单一职责原则，专门处理排名相关逻辑
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>用户排名实体</returns>
    private async Task<UserRanking> GetOrCreateUserRankingAsync(Guid userId)
    {
        var userRanking = _context.UserRankings.Local.FirstOrDefault(ur => ur.UserId == userId)
            ?? await _context.UserRankings.FirstOrDefaultAsync(ur => ur.UserId == userId);

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
        var userDailyReward = _context.UserDailyRewards.Local.FirstOrDefault(udr => udr.UserId == userId)
            ?? await _context.UserDailyRewards.FirstOrDefaultAsync(udr => udr.UserId == userId);

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
        var cards = new List<UserCard>();
        var random = new Random();

        // 获取所有可收集的卡片
        var setDefinitionId = userCardPack.CardPackDefinition!.CardSetDefinitionId;
        var availableCards = await _context.CardDefinitions
            .Where(cd => cd.IsCollectible && cd.CardSetDefinitionId == setDefinitionId)
            .ToListAsync();

        if (availableCards.Count == 0)
        {
            throw new InvalidOperationException(
                $"No collectible cards for card pack {userCardPack.CardPackDefinitionId}, card set {setDefinitionId}.");
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
