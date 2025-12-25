//
// 相关文档:
// - 经济系统: 文档/05-游戏系统/06-经济系统.md
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// 用户经济实体 - 存储用户的经济信息（金币、排名等）
/// </summary>
public class UserEconomy
{
    /// <summary>
    /// 经济信息唯一标识符
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 关联的用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 当前金币数量
    /// </summary>
    public int Coins { get; set; } = 1000; // 初始金币

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    /// <summary>
    /// 关联的用户
    /// </summary>
    public User User { get; set; } = null!;
}
