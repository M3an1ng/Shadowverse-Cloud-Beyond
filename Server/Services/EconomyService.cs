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
        var setDefinitionId = userCardPack.CardPackDefinition?.CardSetDefinitionId;
        var availableCards = await _context.CardDefinitions
            .Where(cd => cd.IsCollectible && (!setDefinitionId.HasValue || cd.CardSetDefinitionId == setDefinitionId))
            .ToListAsync();

        if (!availableCards.Any())
        {
            availableCards = await _context.CardDefinitions
                .Where(cd => cd.IsCollectible)
                .ToListAsync();
        }

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
