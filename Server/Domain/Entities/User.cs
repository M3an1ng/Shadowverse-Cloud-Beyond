//
// 相关文档:
// - 数据库设计: 文档/04-数据库设计/01-实体关系图.md
// - 经济系统: 文档/05-游戏系统/06-经济系统.md
// - 用户管理系统: 文档/07-开发指南/01-开发环境搭建.md
//
using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// 用户实体 - 表示系统中的用户账户
/// </summary>
public class User
{
    /// <summary>
    /// 用户唯一标识符
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 用户名 (唯一)
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 邮箱地址 (唯一)
    /// </summary>
    [Required]
    [StringLength(255)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 密码哈希值
    /// </summary>
    [Required]
    [StringLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 头像URL (可选)
    /// </summary>
    [StringLength(500)]
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// 个人简介 (可选)
    /// </summary>
    [StringLength(500)]
    public string? Bio { get; set; }

    /// <summary>
    /// 用户角色
    /// </summary>
    public UserRole Role { get; set; } = UserRole.Player;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后登录时间 (可选)
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 邮箱是否已验证
    /// </summary>
    public bool IsEmailVerified { get; set; } = false;

    /// <summary>
    /// 账户是否激活
    /// </summary>
    public bool IsActive { get; set; } = true;

    // 导航属性
    /// <summary>
    /// 用户资料
    /// </summary>
    public UserProfile? Profile { get; set; }

    /// <summary>
    /// 用户经济信息
    /// </summary>
    public UserEconomy? Economy { get; set; }

    /// <summary>
    /// 游戏会话列表
    /// </summary>
    public List<GameSession> GameSessions { get; set; } = new();

    /// <summary>
    /// 金币交易记录
    /// </summary>
    public List<CoinTransaction> CoinTransactions { get; set; } = new();

    /// <summary>
    /// 用户卡包列表
    /// </summary>
    public List<UserCardPack> CardPacks { get; set; } = new();


}

/// <summary>
/// 用户角色枚举
/// </summary>
public enum UserRole
{
    /// <summary>
    /// 普通玩家
    /// </summary>
    Player = 0,

    /// <summary>
    /// 管理员
    /// </summary>
    Admin = 1,

    /// <summary>
    /// 游客 (仅观战模式)
    /// </summary>
    Guest = 2
}
