using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// 任务进度事件 - 用于按任务周期和来源实体去重
/// </summary>
public class QuestProgressEvent
{
    /// <summary>
    /// 事件唯一标识符
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 任务定义ID
    /// </summary>
    public int QuestDefinitionId { get; set; }

    /// <summary>
    /// 用户行为类型
    /// </summary>
    public QuestActivityType ActivityType { get; set; }

    /// <summary>
    /// 任务周期键
    /// </summary>
    [StringLength(32)]
    public string PeriodKey { get; set; } = string.Empty;

    /// <summary>
    /// 来源实体类型，如 Card、Match、Login
    /// </summary>
    [StringLength(32)]
    public string SourceEntityType { get; set; } = string.Empty;

    /// <summary>
    /// 来源实体ID
    /// </summary>
    [StringLength(100)]
    public string SourceEntityId { get; set; } = string.Empty;

    /// <summary>
    /// 本次事件增加的进度
    /// </summary>
    public int Increment { get; set; } = 1;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 用户
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// 任务定义
    /// </summary>
    public QuestDefinition QuestDefinition { get; set; } = null!;
}
