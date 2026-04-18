using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// 卡牌评论
/// </summary>
public class CardComment : BaseUserEntity
{
    /// <summary>
    /// 卡牌定义ID（按卡牌ID共享评论）
    /// </summary>
    public int CardDefinitionId { get; set; }

    /// <summary>
    /// 评论内容
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 关联的卡牌定义
    /// </summary>
    public CardDefinition CardDefinition { get; set; } = null!;
}

