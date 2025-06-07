using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// 用户相关实体的基类
/// 遵循DRY原则，提取公共属性和行为
/// </summary>
public abstract class BaseUserEntity
{
    /// <summary>
    /// 实体唯一标识符
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 关联的用户ID
    /// </summary>
    public Guid UserId { get; set; }

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

    /// <summary>
    /// 更新时间戳
    /// 在任何修改操作后调用此方法
    /// </summary>
    protected void UpdateTimestamp()
    {
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 验证实体状态
    /// 子类可以重写此方法添加特定验证逻辑
    /// </summary>
    /// <returns>验证结果</returns>
    public virtual bool IsValid()
    {
        return UserId != Guid.Empty && CreatedAt <= DateTime.UtcNow;
    }
}
