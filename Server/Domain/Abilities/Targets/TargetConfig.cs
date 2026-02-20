using System.Collections.Generic;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Abilities.Common; // Re-added for SortDirection etc.
using ShadowverseBeyond.Server.Domain.Query; // Added for CardQueryConfig

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets
{
    /// <summary>
    /// Defines when insufficient targets should block ability usage.
    /// </summary>
    public enum TargetHandlingStrategy
    {
        /// <summary>
        /// Do not block on insufficient targets. Ability continues execution.
        /// </summary>
        DoNotIntercept = 0,

        /// <summary>
        /// Block only during actual ability execution.
        /// Playability check will not block player operation.
        /// </summary>
        BlockAbilityExecutionOnly = 1,

        /// <summary>
        /// Block during playability check, so player operation is prevented up-front.
        /// </summary>
        BlockPlayerOperation = 2
    }

    /// <summary>
    /// 目标选择器配置类
    /// 根据M11目标选择系统设计文档实现，支持JSON配置
    /// 简化版：直接嵌入 CardQueryConfig 来处理目标池、筛选、排序和数量
    /// </summary>
    public class TargetConfig
    {
        /// <summary>
        /// 选择器描述
        /// </summary>
        //public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 卡牌查询配置，用于定义目标池、筛选、排序和数量限制。
        /// </summary>
        public QueryConfig Query { get; set; } = new();

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 目标解析顺序（序号越小越先解析）
        /// </summary>
        public int? Order { get; set; }

        // Removed TargetPool, Filters, Count, Ordering properties as their functionality
        // is now handled by the embedded CardQueryConfig 'Query'.

        /// <summary>
        /// 指示在可玩性检查期间，是否必须至少存在一个有效的目标。
        /// 如果为 true，且找不到任何有效目标，则拥有此能力（且需要满足此目标）的卡牌将被视为不可用。
        /// </summary>
        public bool RequireValidTarget { get; set; } = false;

        /// <summary>
        /// Strategy for handling insufficient targets.
        /// Null means "use compatibility behavior" (legacy RequireValidTarget).
        /// </summary>
        [JsonPropertyName("targetHandlingStrategy")]
        public TargetHandlingStrategy? TargetHandling { get; set; } = null;

        /// <summary>
        /// Legacy JSON compatibility: maps old field name to TargetHandlingStrategy.
        /// This member is deserialize-only and omitted on serialization.
        /// </summary>
        [JsonPropertyName("insufficientTargetHandling")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [global::System.Obsolete("Use targetHandlingStrategy instead.")]
        public TargetHandlingStrategy? LegacyInsufficientTargetHandling
        {
            get => null;
            set
            {
                if (value.HasValue && !this.TargetHandling.HasValue)
                {
                    this.TargetHandling = value;
                }
            }
        }

        /// <summary>
        /// Resolves the configured handling strategy with safe fallback.
        /// </summary>
        public TargetHandlingStrategy GetEffectiveTargetHandlingStrategy()
        {
            if (this.TargetHandling.HasValue &&
                global::System.Enum.IsDefined(typeof(TargetHandlingStrategy), this.TargetHandling.Value))
            {
                return this.TargetHandling.Value;
            }

            // Legacy compatibility:
            // previous RequireValidTarget behavior blocked player operation at playability check.
            if (RequireValidTarget)
            {
                return TargetHandlingStrategy.BlockPlayerOperation;
            }

            return TargetHandlingStrategy.DoNotIntercept;
        }

        /// <summary>
        /// Whether insufficient targets should block during playability check.
        /// </summary>
        public bool ShouldBlockPlayerOperationOnInsufficientTargets()
        {
            return GetEffectiveTargetHandlingStrategy() == TargetHandlingStrategy.BlockPlayerOperation;
        }

        /// <summary>
        /// Whether insufficient targets should block during actual ability execution.
        /// </summary>
        public bool ShouldBlockAbilityExecutionOnInsufficientTargets()
        {
            var strategy = GetEffectiveTargetHandlingStrategy();
            return strategy == TargetHandlingStrategy.BlockAbilityExecutionOnly
                || strategy == TargetHandlingStrategy.BlockPlayerOperation;
        }

        /// <summary>
        /// 玩家选择的具体配置
        /// </summary>
        public PlayerChoiceConfig? PlayerChoiceOptions { get; set; }
    }

    // Removed inner classes TargetPoolConfig, TargetFilterConfig, ValueSourceConfig, CountConfig, OrderingConfig.
    // Their functionality is now handled by the embedded CardQueryConfig Query property.

    /// <summary>
    /// 玩家选择配置类
    /// </summary>
    public class PlayerChoiceConfig
    {
        /// <summary>
        /// 是否强制需要玩家选择
        /// </summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// 是否可以取消选择
        /// </summary>
        public bool CanCancel { get; set; } = false;

        /// <summary>
        /// 是否自动选择
        /// </summary>
        public bool AutoSelect { get; set; } = false;

        /// <summary>
        /// 是否反选（选择除指定卡以外的卡）
        /// </summary>
        public bool IsInverseSelection { get; set; } = false;

        /// <summary>
        /// 玩家选择的最小数量限制
        /// </summary>
        public int MinCount { get; set; } = 1;

        /// <summary>
        /// 玩家选择的最大数量限制
        /// </summary>
        public int MaxCount { get; set; } = 1;
    }
}
