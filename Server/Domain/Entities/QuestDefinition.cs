using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// 任务定义实体 - 定义游戏中的各种任务（参考炉石传说任务机制）
/// </summary>
public class QuestDefinition
{
    /// <summary>
    /// 任务定义唯一标识符
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 任务名称
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 任务描述
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 任务类型
    /// </summary>
    public QuestType Type { get; set; }

    /// <summary>
    /// 推进任务进度的用户行为类型
    /// </summary>
    public QuestActivityType ActivityType { get; set; } = QuestActivityType.PlayMatch;

    /// <summary>
    /// 任务分类
    /// </summary>
    public QuestCategory Category { get; set; }

    /// <summary>
    /// 任务进度去重策略
    /// </summary>
    public QuestDeduplicationPolicy DeduplicationPolicy { get; set; } = QuestDeduplicationPolicy.None;

    /// <summary>
    /// 目标值 (如进行3场对战、使用10张法术牌等)
    /// </summary>
    public int TargetValue { get; set; }

    /// <summary>
    /// 金币奖励
    /// </summary>
    public int CoinReward { get; set; }

    /// <summary>
    /// 经验奖励
    /// </summary>
    public int ExperienceReward { get; set; } = 0;

    /// <summary>
    /// 是否激活
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 刷新间隔（小时）
    /// </summary>
    public int RefreshIntervalHours { get; set; } = 24;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    /// <summary>
    /// 用户任务列表
    /// </summary>
    public List<UserQuest> UserQuests { get; set; } = new();

    /// <summary>
    /// 获取任务类型显示名称
    /// </summary>
    /// <returns>类型显示名称</returns>
    public string GetTypeDisplayName()
    {
        return Type switch
        {
            QuestType.PlayGames => "进行对战",
            QuestType.WinGames => "获得胜利",
            QuestType.UseSpells => "使用法术",
            QuestType.SummonFollowers => "召唤随从",
            QuestType.DealDamage => "造成伤害",
            QuestType.Evolution => "进化随从",
            QuestType.PlayClass => "使用职业",
            QuestType.CollectCards => "收集卡片",
            QuestType.OpenPacks => "开启卡包",
            QuestType.LoginDaily => "每日登录",
            _ => "未知类型"
        };
    }

    /// <summary>
    /// 获取任务分类显示名称
    /// </summary>
    /// <returns>分类显示名称</returns>
    public string GetCategoryDisplayName()
    {
        return Category switch
        {
            QuestCategory.Daily => "每日任务",
            QuestCategory.Weekly => "周常任务",
            QuestCategory.Special => "特殊活动",
            QuestCategory.Achievement => "成就任务",
            _ => "未知分类"
        };
    }

    /// <summary>
    /// 获取进度描述
    /// </summary>
    /// <param name="currentProgress">当前进度</param>
    /// <returns>进度描述</returns>
    public string GetProgressDescription(int currentProgress)
    {
        return Type switch
        {
            QuestType.PlayGames => $"进行对战 {currentProgress}/{TargetValue} 场",
            QuestType.WinGames => $"获得胜利 {currentProgress}/{TargetValue} 场",
            QuestType.UseSpells => $"使用法术 {currentProgress}/{TargetValue} 张",
            QuestType.SummonFollowers => $"召唤随从 {currentProgress}/{TargetValue} 个",
            QuestType.DealDamage => $"造成伤害 {currentProgress}/{TargetValue} 点",
            QuestType.Evolution => $"进化随从 {currentProgress}/{TargetValue} 个",
            QuestType.PlayClass => $"使用职业 {currentProgress}/{TargetValue} 场",
            QuestType.CollectCards => $"收集卡片 {currentProgress}/{TargetValue} 张",
            QuestType.OpenPacks => $"开启卡包 {currentProgress}/{TargetValue} 个",
            QuestType.LoginDaily => $"连续登录 {currentProgress}/{TargetValue} 天",
            _ => $"{currentProgress}/{TargetValue}"
        };
    }




}

/// <summary>
/// 任务类型枚举
/// </summary>
public enum QuestType
{
    /// <summary>
    /// 进行对战 (如"进行3场对战")
    /// </summary>
    PlayGames = 1,

    /// <summary>
    /// 胜利对战 (如"获得2场胜利")
    /// </summary>
    WinGames = 2,

    /// <summary>
    /// 使用法术 (如"使用10张法术牌")
    /// </summary>
    UseSpells = 3,

    /// <summary>
    /// 召唤随从 (如"召唤15个随从")
    /// </summary>
    SummonFollowers = 4,

    /// <summary>
    /// 造成伤害 (如"造成100点伤害")
    /// </summary>
    DealDamage = 5,

    /// <summary>
    /// 进化 (如"进化5个随从")
    /// </summary>
    Evolution = 6,

    /// <summary>
    /// 使用特定职业 (如"使用法师获得胜利")
    /// </summary>
    PlayClass = 7,

    /// <summary>
    /// 收集卡片 (如"获得5张新卡片")
    /// </summary>
    CollectCards = 8,

    /// <summary>
    /// 开启卡包 (如"开启3个卡包")
    /// </summary>
    OpenPacks = 9,

    /// <summary>
    /// 每日登录 (如"连续登录3天")
    /// </summary>
    LoginDaily = 10
}

/// <summary>
/// 推进任务进度的用户行为类型
/// </summary>
public enum QuestActivityType
{
    /// <summary>
    /// 参与对战
    /// </summary>
    PlayMatch = 1,

    /// <summary>
    /// 赢得对战
    /// </summary>
    WinMatch = 2,

    /// <summary>
    /// 发表评论
    /// </summary>
    Comment = 3,

    /// <summary>
    /// 点赞或点踩
    /// </summary>
    Vote = 4,

    /// <summary>
    /// 登录
    /// </summary>
    Login = 5,

    /// <summary>
    /// 观战
    /// </summary>
    SpectateMatch = 6
}

/// <summary>
/// 任务进度去重策略
/// </summary>
public enum QuestDeduplicationPolicy
{
    /// <summary>
    /// 每次有效行为都增加进度
    /// </summary>
    None = 0,

    /// <summary>
    /// 同一周期内同一来源实体只计一次
    /// </summary>
    PerSourceEntityPerPeriod = 1,

    /// <summary>
    /// 同一周期只计一次
    /// </summary>
    OncePerPeriod = 2
}

/// <summary>
/// 任务分类枚举
/// </summary>
public enum QuestCategory
{
    /// <summary>
    /// 每日任务
    /// </summary>
    Daily = 1,

    /// <summary>
    /// 周常任务
    /// </summary>
    Weekly = 2,

    /// <summary>
    /// 特殊活动任务
    /// </summary>
    Special = 3,

    /// <summary>
    /// 成就类任务 (一次性)
    /// </summary>
    Achievement = 4
}
