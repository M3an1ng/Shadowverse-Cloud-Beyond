//
// 相关文档:
// - API文档: 文档/06-API文档/01-端点总览.md
// - 经济系统: 文档/05-游戏系统/06-经济系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using Microsoft.AspNetCore.Mvc;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Controllers;

/// <summary>
/// 经济系统API控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EconomyController : ControllerBase
{
    private readonly IEconomyService _economyService;
    private readonly ILogger<EconomyController> _logger;

    public EconomyController(IEconomyService economyService, ILogger<EconomyController> logger)
    {
        _economyService = economyService;
        _logger = logger;
    }

    #region 金币管理

    /// <summary>
    /// 获取用户金币数量
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>金币数量</returns>
    [HttpGet("coins/{userId}")]
    public async Task<ActionResult<int>> GetUserCoins(Guid userId)
    {
        try
        {
            var coins = await _economyService.GetUserCoinsAsync(userId);
            return Ok(coins);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户金币失败: UserId={UserId}", userId);
            return StatusCode(500, "获取金币信息失败");
        }
    }

    /// <summary>
    /// 获取用户金币交易历史
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页大小</param>
    /// <returns>交易历史列表</returns>
    [HttpGet("coins/{userId}/history")]
    public async Task<ActionResult<List<CoinTransactionDto>>> GetCoinHistory(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var history = await _economyService.GetCoinHistoryAsync(userId, page, pageSize);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取金币交易历史失败: UserId={UserId}", userId);
            return StatusCode(500, "获取交易历史失败");
        }
    }

    /// <summary>
    /// 管理员添加金币 (测试用)
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="amount">金币数量</param>
    /// <param name="description">描述</param>
    /// <returns>是否成功</returns>
    [HttpPost("coins/{userId}/add")]
    public async Task<ActionResult<bool>> AddCoins(Guid userId, [FromQuery] int amount, [FromQuery] string? description = null)
    {
        try
        {
            var success = await _economyService.AddCoinsAsync(userId, amount, CoinTransactionType.AdminGrant, description);
            if (success)
            {
                return Ok(true);
            }
            else
            {
                return BadRequest("添加金币失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加金币失败: UserId={UserId}, Amount={Amount}", userId, amount);
            return StatusCode(500, "添加金币失败");
        }
    }

    #endregion

    #region 卡包系统

    /// <summary>
    /// 获取可购买的卡包列表
    /// </summary>
    /// <returns>卡包列表</returns>
    [HttpGet("cardpacks")]
    public async Task<ActionResult<List<CardPackDefinitionDto>>> GetAvailableCardPacks()
    {
        try
        {
            var cardPacks = await _economyService.GetAvailableCardPacksAsync();
            return Ok(cardPacks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取卡包列表失败");
            return StatusCode(500, "获取卡包列表失败");
        }
    }

    /// <summary>
    /// 购买卡包
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cardPackDefinitionId">卡包定义ID</param>
    /// <returns>购买的卡包</returns>
    [HttpPost("cardpacks/{userId}/purchase")]
    public async Task<ActionResult<UserCardPackDto>> PurchaseCardPack(Guid userId, [FromQuery] int cardPackDefinitionId)
    {
        try
        {
            var cardPack = await _economyService.PurchaseCardPackAsync(userId, cardPackDefinitionId);
            if (cardPack != null)
            {
                return Ok(cardPack);
            }
            else
            {
                return BadRequest("购买卡包失败，可能是金币不足或卡包不存在");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "购买卡包失败: UserId={UserId}, CardPackDefinitionId={CardPackDefinitionId}", userId, cardPackDefinitionId);
            return StatusCode(500, "购买卡包失败");
        }
    }

    /// <summary>
    /// 开启卡包
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cardPackId">卡包ID</param>
    /// <returns>获得的卡片列表</returns>
    [HttpPost("cardpacks/{userId}/open")]
    public async Task<ActionResult<List<UserCardDto>>> OpenCardPack(Guid userId, [FromQuery] Guid cardPackId)
    {
        try
        {
            var cards = await _economyService.OpenCardPackAsync(userId, cardPackId);
            return Ok(cards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开启卡包失败: UserId={UserId}, CardPackId={CardPackId}", userId, cardPackId);
            return StatusCode(500, "开启卡包失败");
        }
    }

    /// <summary>
    /// 获取用户卡包列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="unopenedOnly">是否只返回未开启的卡包</param>
    /// <returns>用户卡包列表</returns>
    [HttpGet("cardpacks/{userId}")]
    public async Task<ActionResult<List<UserCardPackDto>>> GetUserCardPacks(Guid userId, [FromQuery] bool unopenedOnly = false)
    {
        try
        {
            var cardPacks = await _economyService.GetUserCardPacksAsync(userId, unopenedOnly);
            return Ok(cardPacks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户卡包列表失败: UserId={UserId}", userId);
            return StatusCode(500, "获取卡包列表失败");
        }
    }

    #endregion

    #region 排名系统

    /// <summary>
    /// 获取用户排名信息
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>用户排名信息</returns>
    [HttpGet("rank/{userId}")]
    public async Task<ActionResult<UserRankDto>> GetUserRank(Guid userId)
    {
        try
        {
            var rank = await _economyService.GetUserRankAsync(userId);
            if (rank != null)
            {
                return Ok(rank);
            }
            else
            {
                return NotFound("用户排名信息不存在");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户排名失败: UserId={UserId}", userId);
            return StatusCode(500, "获取排名信息失败");
        }
    }

    /// <summary>
    /// 更新用户排名 (游戏结束时调用)
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="isWin">是否胜利</param>
    /// <returns>是否成功</returns>
    [HttpPost("rank/{userId}/update")]
    public async Task<ActionResult<bool>> UpdateUserRank(Guid userId, [FromQuery] bool isWin)
    {
        try
        {
            var success = await _economyService.UpdateUserRankAsync(userId, isWin);
            return Ok(success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新用户排名失败: UserId={UserId}, IsWin={IsWin}", userId, isWin);
            return StatusCode(500, "更新排名失败");
        }
    }

    /// <summary>
    /// 获取排行榜
    /// </summary>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页大小</param>
    /// <returns>排行榜列表</returns>
    [HttpGet("leaderboard")]
    public async Task<ActionResult<List<LeaderboardEntryDto>>> GetLeaderboard([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            var leaderboard = await _economyService.GetLeaderboardAsync(page, pageSize);
            return Ok(leaderboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取排行榜失败: Page={Page}, PageSize={PageSize}", page, pageSize);
            return StatusCode(500, "获取排行榜失败");
        }
    }

    #endregion

    #region 成就系统

    /// <summary>
    /// 获取用户任务列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>用户任务列表</returns>
    [HttpGet("quests/{userId}")]
    public async Task<ActionResult<List<UserQuestDto>>> GetUserQuests(Guid userId)
    {
        try
        {
            var quests = await _economyService.GetUserQuestsAsync(userId);
            return Ok(quests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户任务失败: UserId={UserId}", userId);
            return StatusCode(500, "获取任务列表失败");
        }
    }

    /// <summary>
    /// 更新任务进度 (测试用)
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="type">任务类型</param>
    /// <param name="increment">增加的进度值</param>
    /// <returns>是否有任务完成</returns>
    [HttpPost("quests/{userId}/progress")]
    public async Task<ActionResult<bool>> UpdateQuestProgress(Guid userId, [FromQuery] QuestType type, [FromQuery] int increment = 1)
    {
        try
        {
            var anyCompleted = await _economyService.UpdateQuestProgressAsync(userId, type, increment);
            return Ok(anyCompleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新任务进度失败: UserId={UserId}, Type={Type}", userId, type);
            return StatusCode(500, "更新任务进度失败");
        }
    }

    #endregion

    #region 每日奖励

    /// <summary>
    /// 领取每日奖励
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>每日奖励信息</returns>
    [HttpPost("daily-reward/{userId}/claim")]
    public async Task<ActionResult<DailyRewardDto>> ClaimDailyReward(Guid userId)
    {
        try
        {
            var reward = await _economyService.ClaimDailyRewardAsync(userId);
            if (reward != null)
            {
                return Ok(reward);
            }
            else
            {
                return BadRequest("今日已领取每日奖励或领取失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "领取每日奖励失败: UserId={UserId}", userId);
            return StatusCode(500, "领取每日奖励失败");
        }
    }

    /// <summary>
    /// 检查是否可以领取每日奖励
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>是否可以领取</returns>
    [HttpGet("daily-reward/{userId}/can-claim")]
    public async Task<ActionResult<bool>> CanClaimDailyReward(Guid userId)
    {
        try
        {
            var canClaim = await _economyService.CanClaimDailyRewardAsync(userId);
            return Ok(canClaim);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查每日奖励状态失败: UserId={UserId}", userId);
            return StatusCode(500, "检查每日奖励状态失败");
        }
    }

    #endregion
}
