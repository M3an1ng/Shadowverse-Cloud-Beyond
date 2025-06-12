using ShadowverseBeyond.Server.Domain.Entities;
// Removed duplicate using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // Added for TargetId
using ShadowverseBeyond.Server.Domain.Events; // Added for GameContext
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
{

    /// <summary>
    /// GameState动作结果
    /// </summary>
    public class GameStateActionResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// 生成的事件列表 (强类型为 IGameEvent)
        /// </summary>
        public List<IGameEvent> GeneratedEvents { get; set; } = new(); // Changed type to List<IGameEvent>

        /// <summary>
        /// 返回值（如果有）
        /// </summary>
        public object? ReturnValue { get; set; }

        /// <summary>
        /// 执行时间（毫秒）
        /// </summary>
        public long ExecutionTimeMs { get; set; } = 0;

        /// <summary>
        /// Optional: Detailed results for each target affected by the action.
        /// </summary>
        public List<IndividualActionResult> Results { get; set; } = new(); // Added Results property
    }

    /// <summary>
    /// Represents the result of an action applied to a single target.
    /// </summary>
    public class IndividualActionResult // Added IndividualActionResult class
    {
        /// <summary>
        /// The ID of the target affected.
        /// </summary>
        public TargetId TargetId { get; set; } // Assuming TargetId is the correct type

        /// <summary>
        /// Whether the action was successful for this specific target.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// An optional message providing details about the outcome for this target.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// List of game events generated specifically by the action on this individual target.
        /// </summary>
        public List<IGameEvent> GeneratedEvents { get; set; } = new List<IGameEvent>(); // Added GeneratedEvents property

        // Add other relevant details if needed, e.g., state changes specific to the target
    }

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
