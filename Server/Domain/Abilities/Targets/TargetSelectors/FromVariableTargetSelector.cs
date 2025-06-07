using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetSelectors
{
    /// <summary>
    /// 从变量获取目标选择器
    /// 从 AbilityVariables 中获取目标列表
    /// 根据M11目标选择系统设计文档实现
    /// </summary>
    public class FromVariableTargetSelector : ITargetSelector
    {
        private readonly TargetConfig _config;

        public FromVariableTargetSelector(TargetConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        // Match interface signature
        public Task<List<TargetId>> SelectTargetsAsync(TargetConfig config, AbilityContext context)
        {
            // Use config passed in method, not the one stored in _config field for consistency
            if (string.IsNullOrEmpty(config.VariableName))
            {
                // Log warning?
                return Task.FromResult(new List<TargetId>());
            }

            // Access Variables from AbilityContext
            if (context.Variables == null || !context.Variables.TryGetValue(config.VariableName, out var variableValue))
            {
                // Variable not found
                return Task.FromResult(new List<TargetId>());
            }

            // Attempt to cast the variable value to a list of TargetId
            if (variableValue is List<TargetId> targetList)
            {
                return Task.FromResult(targetList);
            }
            else if (variableValue is IEnumerable<TargetId> targetEnumerable)
            {
                // Handle if it's stored as IEnumerable
                return Task.FromResult(targetEnumerable.ToList());
            }
            else
            {
                // Log error? Variable found but wrong type
                return Task.FromResult(new List<TargetId>());
            }
        }
    }
}
