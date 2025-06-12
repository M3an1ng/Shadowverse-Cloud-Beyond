using ShadowverseBeyond.Server.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Buffs
{
    /// <summary>
    /// Defines the contract for managing Buff instances within a game session.
    /// Responsible for registering, unregistering, and querying active buffs.
    /// </summary>
    public interface IBuffManager
    {
        /// <summary>
        /// Registers a new Buff instance, making its effects potentially active.
        /// The manager should store the buff and handle its lifecycle based on its duration and removal conditions.
        /// </summary>
        /// <param name="buff">The Buff instance to register.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RegisterBuffAsync(Buff buff);

        /// <summary>
        /// Unregisters a specific Buff instance, typically when it expires or is explicitly removed.
        /// </summary>
        /// <param name="buffId">The unique ID of the Buff to unregister.</param>
        /// <returns>True if the buff was found and removed, false otherwise.</returns>
        Task<bool> UnregisterBuffAsync(Guid buffId);

        /// <summary>
        /// Retrieves all active Buff instances currently applied to a specific card instance.
        /// The returned list should ideally be ordered by application time, as calculation order matters.
        /// </summary>
        /// <param name="cardInstanceId">The ID of the target card instance.</param>
        /// <returns>An ordered list of active Buffs applied to the card.</returns>
        Task<List<Buff>> GetBuffsForCardAsync(Guid cardInstanceId);

        /// <summary>
        /// Removes all Buffs that originated from a specific source ability instance.
        /// Useful when the source card leaves play (e.g., for auras).
        /// </summary>
        /// <param name="sourceAbilityId">The unique ID of the source ability.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RemoveBuffsFromSourceAsync(string sourceAbilityId); // Assuming AbilityId is long based on Ability.cs

        /// <summary>
        /// Removes all Buffs currently applied to a specific card instance.
        /// Typically used by Silence effects.
        /// </summary>
        /// <param name="cardInstanceId">The ID of the target card instance.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RemoveAllBuffsAsync(Guid cardInstanceId);

        // Optional: Add methods for querying buffs by tag, source type, etc. if needed.
        // Optional: Add methods for handling buff updates/refreshes if needed.

        /// <summary>
        /// 应用一个Buff到目标卡牌实例。
        /// </summary>
        /// <param name="target">目标卡牌实例。</param>
        /// <param name="buffDefinitionId">要应用的Buff的定义ID或名称。</param>
        /// <param name="sourceCard">Buff的来源卡牌实例（可选）。</param>
        /// <param name="duration">Buff的持续时间（回合数，null表示永久或由Buff定义决定）。</param>
        /// <param name="parameters">传递给Buff的额外参数。</param>
        /// <returns>一个包含操作结果的任务，包括是否成功、应用的Buff实例（如果成功）以及可能产生的游戏事件。</returns>
        Task<BuffApplicationResult> ApplyBuffAsync(
            Entities.CardInstance target,
            string buffDefinitionId,
            Entities.CardInstance? sourceCard,
            int? duration,
            Dictionary<string, object?> parameters);

        /// <summary>
        /// 创建并应用一个Buff到目标卡牌实例。
        /// </summary>
        /// <param name="target">目标卡牌实例</param>
        /// <param name="sourceCard">源卡牌实例</param>
        /// <param name="sourceAbility">源能力实例</param>
        /// <param name="buffType">Buff类型</param>
        /// <param name="parameters">Buff参数</param>
        /// <param name="durationType">持续时间类型</param>
        /// <param name="durationParameter">持续时间参数</param>
        /// <param name="currentTurn">当前回合数</param>
        /// <returns>Buff应用结果</returns>
        Task<BuffApplicationResult> CreateAndApplyBuffAsync(
            Entities.CardInstance target,
            Entities.CardInstance? sourceCard,
            Abilities.Ability sourceAbility,
            string buffType,
            Dictionary<string, object?> parameters,
            Abilities.Common.DurationType durationType,
            object? durationParameter,
            int currentTurn);

        /// <summary>
        /// 对目标卡牌应用沉默效果
        /// </summary>
        /// <param name="target">目标卡牌实例</param>
        /// <param name="sourceCard">施放沉默的卡牌实例（可选）</param>
        /// <param name="sourceAbility">施放沉默的能力实例（可选）</param>
        /// <returns>沉默应用结果</returns>
        Task<SilenceApplicationResult> ApplySilenceAsync(
            Entities.CardInstance target,
            Entities.CardInstance? sourceCard = null,
            Abilities.Ability? sourceAbility = null);

        /// <summary>
        /// 移除目标卡牌的沉默效果
        /// </summary>
        /// <param name="target">目标卡牌实例</param>
        /// <returns>沉默移除结果</returns>
        Task<SilenceRemovalResult> RemoveSilenceAsync(Entities.CardInstance target);
    }

    /// <summary>
    /// Represents the result of attempting to apply a buff.
    /// </summary>
    public class BuffApplicationResult
    {
        /// <summary>
        /// Indicates whether the buff application was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// An optional error message if the application failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// The actual Buff instance that was created and applied, if successful.
        /// </summary>
        public Buff? AppliedBuff { get; set; }

        /// <summary>
        /// Any game events generated as a direct result of the buff application (e.g., BuffAppliedEvent).
        /// Using List<object> for now, consider using IGameEvent if appropriate.
        /// </summary>
        public List<IGameEvent>? GeneratedEvents { get; set; } = new List<IGameEvent>();
    }

    /// <summary>
    /// Represents the result of attempting to apply silence.
    /// </summary>
    public class SilenceApplicationResult
    {
        /// <summary>
        /// Indicates whether the silence application was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// An optional error message if the application failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// List of buffs that were removed due to silence.
        /// </summary>
        public List<Buff> RemovedBuffs { get; set; } = new List<Buff>();

        /// <summary>
        /// Any game events generated as a direct result of the silence application.
        /// </summary>
        public List<IGameEvent>? GeneratedEvents { get; set; } = new List<IGameEvent>();
    }

    /// <summary>
    /// Represents the result of attempting to remove silence.
    /// </summary>
    public class SilenceRemovalResult
    {
        /// <summary>
        /// Indicates whether the silence removal was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// An optional error message if the removal failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Any game events generated as a direct result of the silence removal.
        /// </summary>
        public List<object>? GeneratedEvents { get; set; } = new List<object>();
    }
}