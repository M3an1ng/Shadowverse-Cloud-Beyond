using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Query; // Add missing using
using Microsoft.Extensions.Logging; // Add missing using
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetSelectors
{
    /// <summary>
    /// 组合目标选择器
    /// 组合多个子选择器的结果
    /// 根据M11目标选择系统设计文档实现
    /// </summary>
    public class CompositeTargetSelector : ITargetSelector
    {
        private readonly TargetConfig _config;
        private readonly List<(TargetConfig Config, ITargetSelector Selector)> _subSelectors; // Store config with selector
        private readonly ICardQueryExecutor _queryExecutor; // Added dependency field
        private readonly ILogger<FilteredTargetSelector> _logger; // Added dependency field

        // Replace existing constructor with one that accepts dependencies
        public CompositeTargetSelector(TargetConfig config, ICardQueryExecutor queryExecutor, ILogger<FilteredTargetSelector> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor)); // Assign dependency
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // Assign dependency
            _subSelectors = new List<(TargetConfig Config, ITargetSelector Selector)>(); // Initialize list

            // 创建子选择器并存储其配置
            if (_config.SubSelectors != null) // Add null check for safety
            {
                foreach (var subConfig in _config.SubSelectors)
                {
                    // Pass assigned dependencies when creating sub-selectors
                    var subSelector = TargetSelectorFactory.CreateSelector(subConfig, _queryExecutor, _logger);
                    _subSelectors.Add((subConfig, subSelector)); // Store tuple
                }
            }
        }

        // Match interface signature
        public async Task<List<TargetId>> SelectTargetsAsync(TargetConfig config, GameContext context)
        {
            var allTargets = new List<TargetId>();

            // 执行所有子选择器并合并结果
            foreach (var (subConfig, selector) in _subSelectors) // Deconstruct tuple
            {
                // Pass the specific subConfig and the received GameContext
                var targets = await selector.SelectTargetsAsync(subConfig, context);
                if (targets != null) // Add null check for safety
                {
                    allTargets.AddRange(targets);
                }
            }

            // 去重并转换为List返回
            return allTargets.Distinct().ToList();
        }
    }
}
