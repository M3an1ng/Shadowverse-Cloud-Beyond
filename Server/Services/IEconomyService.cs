using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

/// <summary>
/// 经济系统服务接口 - 处理金币、卡包、排名、成就等经济相关功能
/// </summary>
public interface IEconomyService
{
    // 金币管理
    /// <summary>
    /// 获取用户当前金币数量
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>金币数量</returns>
    Task<int> GetUserCoinsAsync(Guid userId);

    /// <summary>
    /// 添加金币到用户账户
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="amount">金币数量</param>
    /// <param name="type">交易类型</param>
    /// <param name="description">交易描述</param>
    /// <param name="relatedEntityId">关联实体ID</param>
    /// <returns>是否成功</returns>
    Task<bool> AddCoinsAsync(Guid userId, int amount, CoinTransactionType type, string? description = null, string? relatedEntityId = null);

    /// <summary>
    /// 从用户账户扣除金币
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="amount">金币数量</param>
    /// <param name="type">交易类型</param>
    /// <param name="description">交易描述</param>
    /// <param name="relatedEntityId">关联实体ID</param>
    /// <returns>是否成功</returns>
    Task<bool> SpendCoinsAsync(Guid userId, int amount, CoinTransactionType type, string? description = null, string? relatedEntityId = null);

    /// <summary>
    /// 获取用户金币交易历史
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页大小</param>
    /// <returns>交易历史列表</returns>
    Task<List<CoinTransactionDto>> GetCoinHistoryAsync(Guid userId, int page = 1, int pageSize = 20);

    // 卡包系统
    /// <summary>
    /// 获取可购买的卡包列表
    /// </summary>
    /// <param name="environment">可选环境过滤</param>
    /// <returns>卡包定义列表</returns>
    Task<List<CardPackDefinitionDto>> GetAvailableCardPacksAsync(DeckEnvironment? environment = null, bool includeInactive = false);

    /// <summary>
    /// 购买卡包
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cardPackDefinitionId">卡包定义ID</param>
    /// <returns>购买的卡包</returns>
    Task<UserCardPackDto?> PurchaseCardPackAsync(Guid userId, int cardPackDefinitionId);

    /// <summary>
    /// 开启卡包
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cardPackId">卡包ID</param>
    /// <returns>获得的卡片列表</returns>
    Task<List<UserCardDto>> OpenCardPackAsync(Guid userId, Guid cardPackId);

    /// <summary>
    /// 获取用户的卡包列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="unopenedOnly">是否只返回未开启的卡包</param>
    /// <returns>用户卡包列表</returns>
    Task<List<UserCardPackDto>> GetUserCardPacksAsync(Guid userId, bool unopenedOnly = false);

    // 排名系统
    /// <summary>
    /// 获取用户排名信息
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>用户排名信息</returns>
    Task<UserRankDto?> GetUserRankAsync(Guid userId);

    /// <summary>
    /// 获取用户元进度状态（金币/任务/排行/每日奖励）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="environment">可选环境过滤（all 表示不过滤）</param>
    /// <param name="deckRule">可选构筑规则过滤（all 表示不过滤）</param>
    /// <returns>元进度聚合视图</returns>
    Task<MetaProgressionStateDto> GetMetaProgressionStateAsync(Guid userId, string? environment = null, string? deckRule = null);

    /// <summary>
    /// 更新用户排名
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="isWin">是否胜利</param>
    /// <returns>是否成功</returns>
    Task<bool> UpdateUserRankAsync(Guid userId, bool isWin);

    /// <summary>
    /// 获取排行榜
    /// </summary>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页大小</param>
    /// <returns>排行榜列表</returns>
    Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(int page = 1, int pageSize = 50);

    // 成就系统
    /// <summary>
    /// 获取用户任务列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>用户任务列表</returns>
    Task<List<UserQuestDto>> GetUserQuestsAsync(Guid userId);

    /// <summary>
    /// 更新任务进度
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="type">任务类型</param>
    /// <param name="increment">增加的进度值</param>
    /// <returns>是否有任务完成</returns>
    Task<bool> UpdateQuestProgressAsync(Guid userId, QuestType type, int increment = 1);

    /// <summary>
    /// 领取任务奖励
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="userQuestId">用户任务ID</param>
    /// <returns>奖励领取结果；若不可领取则返回 null</returns>
    Task<QuestRewardClaimDto?> ClaimQuestRewardAsync(Guid userId, Guid userQuestId);

    /// <summary>
    /// 检查并完成任务
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>完成的任务列表</returns>
    Task<List<UserQuestDto>> CheckAndCompleteQuestsAsync(Guid userId);

    // 每日奖励
    /// <summary>
    /// 领取每日奖励
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>每日奖励信息</returns>
    Task<DailyRewardDto?> ClaimDailyRewardAsync(Guid userId);

    /// <summary>
    /// 检查是否可以领取每日奖励
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>是否可以领取</returns>
    Task<bool> CanClaimDailyRewardAsync(Guid userId);

    // 用户经济信息
    /// <summary>
    /// 获取或创建用户经济信息
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>用户经济信息</returns>
    Task<UserEconomy> GetOrCreateUserEconomyAsync(Guid userId);

    /// <summary>
    /// 初始化新用户的经济信息
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>是否成功</returns>
    Task<bool> InitializeUserEconomyAsync(Guid userId);
}

