using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// 卡牌赞踩
/// </summary>
public class CardVote : BaseUserEntity
{
    /// <summary>
    /// 卡牌定义ID（按卡牌ID共享赞踩）
    /// </summary>
    public int CardDefinitionId { get; set; }

    /// <summary>
    /// 点赞/点踩类型
    /// </summary>
    [Required]
    public CardVoteType VoteType { get; set; }

    /// <summary>
    /// 关联的卡牌定义
    /// </summary>
    public CardDefinition CardDefinition { get; set; } = null!;
}

public enum CardVoteType
{
    Downvote = -1,
    Upvote = 1
}

