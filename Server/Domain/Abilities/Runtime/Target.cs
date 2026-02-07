using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos.Enums;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static ShadowverseBeyond.Server.Domain.Abilities.Common.SelectorType;

namespace ShadowverseBeyond.Server.Domain.Abilities.Runtime
{
    /// <summary>
    /// 运行时目标包装类
    /// 包装TargetConfig并添加当前目标解析
    /// </summary>
    public class Target
    {
        /// <summary>
        /// 静态目标配置
        /// </summary>
        public TargetConfig Config { get; }

        /// <summary>
        /// The key that identifies this target within the parent ability.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// 当前解析的目标
        /// </summary>
        public List<TargetInfo> ResolvedTargets { get; private set; } = [];

        /// <summary>
        /// 目标解析次数
        /// </summary>
        public int ResolutionCount { get; private set; }

        /// <summary>
        /// 上次解析时间
        /// </summary>
        public DateTime? LastResolutionTime { get; private set; }

        /// <summary>
        /// 是否需要重新解析
        /// </summary>
        public bool NeedsResolution { get; private set; } = true;

        /// <summary>
        /// 上次解析所绑定的触发事件ID
        /// </summary>
        public Guid? LastResolvedEventId { get; private set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 目标解析历史
        /// </summary>
        public List<TargetResolutionLogEntry> ResolutionHistory { get; } = [];

        private readonly TargetSelectionService _targetSelectionService;
        private readonly ILogger<Target> _logger;

        public Target(string key, TargetConfig config, TargetSelectionService targetSelectionService, ILogger<Target> logger)
        {
            Key = key;
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _targetSelectionService = targetSelectionService ?? throw new ArgumentNullException(nameof(targetSelectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }


        private void RecordResolutionResult(TargetResolutionLogEntry result)
        {
            ResolutionHistory.Add(result);
            if (ResolutionHistory.Count > 50)
            {
                ResolutionHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// Sets the resolved targets from an external source, bypassing the internal resolution logic.
        /// This is used when the final targets are determined by a higher-level process, such as player choice validation.
        /// </summary>
        /// <param name="targets">The final list of resolved targets.</param>
        /// <param name="triggeringEventId">The triggering event ID used for this resolution.</param>
        public void SetResolvedTargets(List<TargetInfo> targets, Guid? triggeringEventId)
        {
            ResolvedTargets = targets ?? new List<TargetInfo>();
            NeedsResolution = false; // Mark as resolved
            LastResolvedEventId = triggeringEventId;
        }

        public void MarkForResolution()
        {
            NeedsResolution = true;
            LastResolvedEventId = null;
        }

        public void Reset()
        {
            ResolvedTargets.Clear();
            ResolutionCount = 0;
            LastResolutionTime = null;
            NeedsResolution = true;
            ResolutionHistory.Clear();
            LastResolvedEventId = null;
        }

        public bool AreTargetsValid(GameContext context)
        {
            if (NeedsResolution) return false;
            if (context == null)
            {
                _logger.LogWarning("AreTargetsValid called with null GameContext.");
                return ResolvedTargets.Any();
            }
            return ResolvedTargets.All(targetInfo => IsValidTarget(targetInfo, context));
        }

        private bool IsValidTarget(TargetInfo targetInfo, GameContext context)
        {
            if (targetInfo == null) return false;

            if (targetInfo.Type == TargetType.Card)
            {
                var card = targetInfo.Card;
                if (card == null) return false;
                // A card is generally invalid if it's destroyed (health <= 0), unless it's an amulet.
                if ((card.IsFollower || card.IsLeader) && card.CurrentHealth <= 0) return false;
            }
            else if (targetInfo.Type == TargetType.Player)
            {
                var player = targetInfo.Player;
                if (player == null || player.Leader == null || player.Leader.CurrentHealth <= 0) return false;
            }
            return true;
        }
    }



    /// <summary>
    /// 目标解析的历史日志条目
    /// </summary>
    public class TargetResolutionLogEntry
    {
        /// <summary>
        /// 是否成功解析
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 解析的目标
        /// </summary>
        public List<TargetInfo> Targets { get; set; } = [];

        /// <summary>
        /// 错误消息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 解析时间
        /// </summary>
        public DateTime ResolutionTime { get; set; }

        /// <summary>
        /// 解析持续时间
        /// </summary>
        public TimeSpan Duration { get; set; }
    }
}
