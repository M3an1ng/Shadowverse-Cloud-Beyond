using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Common; // Added for AbilityVariables
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using System.Collections.Generic;
using System.Threading.Tasks;

using ShadowverseBeyond.Server.Domain.Abilities; // Added for AbilityContext

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
{
    /// <summary>
    /// Defines the contract for effect handlers within the ability system.
    /// Implementations are responsible for interpreting EffectConfig and executing
    /// the corresponding game state changes via IGameStateActionDispatcher, using
    /// resolved values from IValueSourceResolver.
    /// Based on M12 Effect System Design Document (adapted for AbilityContext).
    /// </summary>
    public interface IEffectHandler
    {
        /// <summary>
        /// Asynchronously executes a single effect based on its configuration and the runtime context.
        /// This is the primary method for effect execution.
        /// </summary>
        /// <param name="config">The static configuration defining the effect to execute.</param>
        /// <param name="context">The runtime context containing game state, source, targets, resolved parameters, etc.</param>
        /// <returns>An EffectExecutionResult indicating the outcome of the execution.</returns>
        Task<EffectExecutionResult> ExecuteAsync(EffectConfig config, AbilityContext context);
        // Note: ExecuteEffectChainAsync might be handled within EventProcessor or a higher-level service now.
    }

    // EffectExecutionParams class removed as it's replaced by AbilityContext

    /// <summary>
    /// 效果执行结果
    /// </summary>
    public class EffectExecutionResult
    {
        /// <summary>
        /// 是否执行成功
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// 执行的效果数量
        /// </summary>
        public int ExecutedEffectsCount { get; set; } = 0;

        /// <summary>
        /// 失败的效果数量
        /// </summary>
        public int FailedEffectsCount { get; set; } = 0;

        /// <summary>
        /// 错误信息
        /// </summary>
        public List<string> ErrorMessages { get; set; } = new();

        /// <summary>
        /// 生成的事件列表
        /// </summary>
        public List<object> GeneratedEvents { get; set; } = new();

        /// <summary>
        /// 更新的变量
        /// </summary>
        public Dictionary<string, object> UpdatedVariables { get; set; } = new(); // Replaced AbilityVariables with Dictionary

        /// <summary>
        /// 执行时间（毫秒）
        /// </summary>
        public long ExecutionTimeMs { get; set; } = 0;
    }
}
