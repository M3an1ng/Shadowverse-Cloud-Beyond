using ShadowverseBeyond.Server.Domain.Entities;
// Removed duplicate using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // Added for TargetId
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
{
    /// <summary>
    /// GameState动作调度器接口
    /// 根据M12设计文档实现，负责将效果类型映射到GameState的原子操作方法
    /// </summary>
    public interface IGameStateActionDispatcher
    {
        /// <summary>
        /// 异步调度 GameState 原子操作。
        /// </summary>
        /// <param name="effectType">要执行的效果类型 (作为动作标识)。</param>
        /// <param name="context">当前的能力上下文，包含执行所需的所有信息。</param>
        /// <returns>GameState 操作的结果。</returns>
        Task<GameStateActionResult> DispatchAsync(string effectType, AbilityContext context); // Updated signature

        /// <summary>
        /// 注册效果类型处理器委托。
        /// </summary>
        /// <param name="effectType">效果类型。</param>
        /// <param name="handler">处理该效果类型的委托，接收 AbilityContext 并返回 Task<GameStateActionResult>。</param>
        void RegisterHandler(string effectType, Func<AbilityContext, Task<GameStateActionResult>> handler); // Updated signature

        /// <summary>
        /// 获取支持的效果类型列表。
        /// </summary>
        /// <returns>支持的效果类型字符串集合。</returns>
        IEnumerable<string> GetSupportedEffectTypes();
    }

    // GameStateActionParameters class removed as its functionality is now covered by AbilityContext.

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
        /// 生成的事件列表
        /// </summary>
        public List<object> GeneratedEvents { get; set; } = new();

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

        // Add other relevant details if needed, e.g., state changes specific to the target
    }


    /// <summary>
    /// 标准效果类型常量
    /// 根据M12设计文档定义的标准效果类型
    /// </summary>
    public static class StandardEffectTypes
    {
        // 伤害相关
        public const string Damage = "Damage";
        public const string Heal = "Heal";
        public const string ModifyHealth = "ModifyHealth";

        // 属性修改
        public const string ModifyAttack = "ModifyAttack";
        public const string ModifyStats = "ModifyStats";
        public const string AddKeyword = "AddKeyword";
        public const string RemoveKeyword = "RemoveKeyword";

        // 卡牌操作
        public const string DrawCard = "DrawCard";
        public const string DiscardCard = "DiscardCard";
        public const string AddCardToHand = "AddCardToHand";
        public const string SummonUnit = "SummonUnit";
        public const string DestroyUnit = "DestroyUnit";

        // 资源操作
        public const string ModifyPP = "ModifyPP";
        public const string ModifyEP = "ModifyEP";
        public const string GainPPOrb = "GainPPOrb";

        // 特殊效果
        public const string Transform = "Transform";
        public const string Evolve = "Evolve";
        public const string Silence = "Silence";
        public const string Freeze = "Freeze";

        // 游戏流程
        public const string EndTurn = "EndTurn";
        public const string SkipTurn = "SkipTurn";
        public const string ExtraTurn = "ExtraTurn";

        // 变量操作
        public const string SetVariable = "SetVariable";
        public const string ModifyVariable = "ModifyVariable";
    }
}
