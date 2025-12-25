using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// 用户收藏的 DSL 表达式
/// </summary>
public class AbilityDslFavorite : BaseUserEntity
{
    /// <summary>
    /// 收藏名称
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// DSL 表达式
    /// </summary>
    [Required]
    [Column(TypeName = "TEXT")]
    public string Expression { get; set; } = string.Empty;

    /// <summary>
    /// 期望类型（可选）
    /// </summary>
    [MaxLength(200)]
    public string? ExpectedType { get; set; }

    /// <summary>
    /// 标签（逗号分隔）
    /// </summary>
    [MaxLength(500)]
    public string Tags { get; set; } = string.Empty;

    /// <summary>
    /// 使用次数
    /// </summary>
    public int UsageCount { get; set; }

    /// <summary>
    /// 最近使用时间
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    public void UpdateUsage()
    {
        UsageCount++;
        LastUsedAt = DateTime.UtcNow;
        UpdateTimestamp();
    }

    public List<string> GetTagList()
    {
        if (string.IsNullOrWhiteSpace(Tags))
        {
            return new List<string>();
        }

        return Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();
    }

    public void SetTagList(IEnumerable<string>? tags)
    {
        if (tags == null)
        {
            Tags = string.Empty;
            UpdateTimestamp();
            return;
        }

        Tags = string.Join(", ", tags.Where(tag => !string.IsNullOrWhiteSpace(tag)));
        UpdateTimestamp();
    }

    public void Rename(string label)
    {
        Label = label;
        UpdateTimestamp();
    }
}
