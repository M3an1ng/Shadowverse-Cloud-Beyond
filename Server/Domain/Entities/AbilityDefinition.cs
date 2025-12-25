using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Entities
{
    /// <summary>
    /// 数据库实体，表示卡牌能力的配置数据
    /// 支持混合存储方案：字符串表达式、JSON结构、DSL源码
    /// </summary>
    [Table("CardAbilities")]
    public class AbilityDefinition
    {
        /// <summary>
        /// 能力的唯一标识符
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// 关联的卡牌定义ID
        /// </summary>
        [Required]
        public int CardId { get; set; }

        /// <summary>
        /// 能力名称
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 能力描述
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// 能力标签（如 "Fanfare", "Rush", "LastWords"）
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string? AbilityTag { get; set; } = string.Empty;

        /// <summary>
        /// 能力类型（枚举值）
        /// </summary>
        [Required]
        public int AbilityType { get; set; }

        /// <summary>
        /// JSON配置（当ConfigFormat为JSON或Mixed时使用）
        /// </summary>
        [Column(TypeName = "TEXT")]
        public string? JsonConfig { get; set; }

        // 注意：移除了简单字符串表达式字段（TriggerExpression, ConditionExpression, TargetExpression, EffectExpression）
        // 这些字段与DSL/JSON配置重复，可能造成混淆。统一使用DSL或JSON格式存储复杂配置。

        /// <summary>
        /// 能力是否激活
        /// </summary>
        [Required]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// 优先级（用于排序）
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// 创建时间
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 最后更新时间
        /// </summary>
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 版本号（用于乐观锁）
        /// </summary>
        [Timestamp]
        public byte[]? Version { get; set; }

        /// <summary>
        /// 导航属性：关联的卡牌定义
        /// </summary>
        [ForeignKey(nameof(CardId))]
        public virtual CardDefinition? CardDefinition { get; set; }

        /// <summary>
        /// 更新时间戳
        /// </summary>
        public void UpdateTimestamp()
        {
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
