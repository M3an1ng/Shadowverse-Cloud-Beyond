//
// 相关文档:
// - Buff系统设计: 文档/05-游戏系统/10-动态效果与Buff系统.md
// - 数据加载优化: 文档/08-项目管理/05-优化核心游戏数据加载机制.md
//
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShadowverseBeyond.Server.Domain.Entities
{
    /// <summary>
    /// BuffDefinition实体类，表示预定义的Buff模板
    /// 支持语义化ID、JSON配置、DSL表达式等多种配置方式
    /// </summary>
    [Table("BuffDefinitions")]
    public class BuffDefinition
    {
        /// <summary>
        /// 语义化ID（如"rush_buff"、"attack_boost_1"）
        /// </summary>
        [Key]
        [Required]
        [MaxLength(100)]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 显示名称
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 描述信息
        /// </summary>
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Buff类型分类（如"Keyword"、"StatModification"、"SpecialEffect"）
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string BuffType { get; set; } = string.Empty;

        /// <summary>
        /// JSON配置（包含效果、持续时间等详细配置）
        /// </summary>
        [Column(TypeName = "TEXT")]
        public string JsonConfig { get; set; } = string.Empty;

        /// <summary>
        /// DSL表达式（可选，用于复杂的Buff逻辑）
        /// </summary>
        [Column(TypeName = "TEXT")]
        public string? DslExpression { get; set; }

        /// <summary>
        /// 是否不可叠加（默认false，允许叠加）
        /// </summary>
        public bool IsNonStackable { get; set; } = false;

        /// <summary>
        /// 默认持续时间类型
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string DefaultDurationType { get; set; } = "Permanent";

        /// <summary>
        /// 默认持续时间参数（如回合数）
        /// </summary>
        public int? DefaultDurationParameter { get; set; }

        /// <summary>
        /// 优先级（用于Buff应用顺序）
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// 稀有度（用于分类和筛选）
        /// </summary>
        [MaxLength(20)]
        public string Rarity { get; set; } = "Common";

        /// <summary>
        /// 标签（用于分类和搜索，逗号分隔）
        /// </summary>
        [MaxLength(500)]
        public string Tags { get; set; } = string.Empty;

        /// <summary>
        /// 是否激活
        /// </summary>
        public bool IsActive { get; set; } = true;

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
        public byte[]? RowVersion { get; set; }

        /// <summary>
        /// 更新时间戳
        /// </summary>
        public void UpdateTimestamp()
        {
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 获取标签列表
        /// </summary>
        /// <returns>标签列表</returns>
        public List<string> GetTagList()
        {
            if (string.IsNullOrWhiteSpace(Tags))
                return new List<string>();

            return Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(tag => tag.Trim())
                      .Where(tag => !string.IsNullOrEmpty(tag))
                      .ToList();
        }

        /// <summary>
        /// 设置标签列表
        /// </summary>
        /// <param name="tags">标签列表</param>
        public void SetTagList(IEnumerable<string> tags)
        {
            Tags = string.Join(", ", tags.Where(tag => !string.IsNullOrWhiteSpace(tag)));
        }

        /// <summary>
        /// 检查是否包含指定标签
        /// </summary>
        /// <param name="tag">要检查的标签</param>
        /// <returns>是否包含该标签</returns>
        public bool HasTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            var tagList = GetTagList();
            return tagList.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 验证BuffDefinition的有效性
        /// </summary>
        /// <returns>验证结果</returns>
        public (bool IsValid, List<string> Errors) Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Id))
                errors.Add("Id cannot be empty");

            if (string.IsNullOrWhiteSpace(Name))
                errors.Add("Name cannot be empty");

            if (string.IsNullOrWhiteSpace(BuffType))
                errors.Add("BuffType cannot be empty");

            if (string.IsNullOrWhiteSpace(JsonConfig))
                errors.Add("JsonConfig cannot be empty");

            if (string.IsNullOrWhiteSpace(DefaultDurationType))
                errors.Add("DefaultDurationType cannot be empty");

            // 验证持续时间类型
            var validDurationTypes = new[] { "Permanent", "UntilEndOfTurn", "LimitedTurns", "UntilTriggered", "Instant" };
            if (!validDurationTypes.Contains(DefaultDurationType))
                errors.Add($"Invalid DefaultDurationType: {DefaultDurationType}");

            // 验证LimitedTurns类型必须有参数
            if (DefaultDurationType == "LimitedTurns" && (!DefaultDurationParameter.HasValue || DefaultDurationParameter.Value <= 0))
                errors.Add("LimitedTurns duration type requires a positive DefaultDurationParameter");

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// 创建BuffDefinition的副本
        /// </summary>
        /// <returns>BuffDefinition副本</returns>
        public BuffDefinition Clone()
        {
            return new BuffDefinition
            {
                Id = Id,
                Name = Name,
                Description = Description,
                BuffType = BuffType,
                JsonConfig = JsonConfig,
                DslExpression = DslExpression,
                IsNonStackable = IsNonStackable,
                DefaultDurationType = DefaultDurationType,
                DefaultDurationParameter = DefaultDurationParameter,
                Priority = Priority,
                Rarity = Rarity,
                Tags = Tags,
                IsActive = IsActive,
                CreatedAt = CreatedAt,
                UpdatedAt = DateTime.UtcNow
                // 注意：不复制RowVersion，让EF Core自动处理
            };
        }
    }
}
