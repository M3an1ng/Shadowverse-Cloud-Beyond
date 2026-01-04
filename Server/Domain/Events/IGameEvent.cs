//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
//
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Base interface for all game events in the Event-driven ability system.
    /// Events represent atomic game actions that can trigger abilities.
    /// </summary>
    public interface IGameEvent
    {
        /// <summary>
        /// Unique identifier for this event type using strongly-typed enum.
        /// </summary>
        EventType EventType { get; }

        /// <summary>
        /// Unique identifier for this specific event instance.
        /// </summary>
        Guid EventId { get; }

        /// <summary>
        /// Timestamp when the event was created.
        /// </summary>
        DateTime Timestamp { get; }


        /// <summary>
        /// The source of the event, encapsulated in a rich TargetInfo object.
        /// It can be a player, a card, or none (for system events).
        /// </summary>
        TargetInfo? Source { get; }

        /// <summary>
        /// The targets of the event, encapsulated in a list of rich TargetInfo objects.
        /// An event can have one or more targets. The list can be empty if there are no targets.
        /// </summary>
        List<TargetInfo> Target { get; }

        /// <summary>
        /// Additional parameters specific to this event type.
        /// </summary>
    }

    /// <summary>
    /// Base abstract class for game events providing common functionality.
    /// </summary>
    [DslExposable("游戏事件基类", Description = "所有游戏事件的基类，提供通用功能")]
    public abstract class GameEventBase : IGameEvent
    {
        [DslExposable("事件类型", Description = "事件的类型标识")]
        public EventType EventType { get; protected set; }

        [DslExposable("事件ID", Description = "事件的唯一标识符")]
        public Guid EventId { get; }

        [DslExposable("时间戳", Description = "事件创建的时间戳")]
        public DateTime Timestamp { get; }

        [DslExposable("事件源", Description = "事件的来源（玩家或卡牌）")]
        public TargetInfo? Source { get; protected set; }

        [DslExposable("事件目标", Description = "事件的目标列表（玩家或卡牌）")]
        public List<TargetInfo> Target { get; protected set; } = new();

        protected GameEventBase(EventType eventType, Guid? eventId = null, DateTime? timestamp = null)
        {
            EventType = eventType;
            EventId = eventId ?? Guid.NewGuid();
            Timestamp = timestamp ?? DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents a game event that can be committed to alter the game state.
    /// This interface is central to the replayable event system, ensuring that
    /// events are pure data carriers and the commit logic is explicitly defined.
    /// </summary>
    public interface ICommittableEvent : IGameEvent
    {
        /// <summary>
        /// Executes the state change associated with this event.
        /// This method should contain the logic to call the appropriate
        /// method on a domain entity to apply the state change.
        /// </summary>
        /// <param name="context">The current game context, providing access to the game state and services.</param>
        /// <returns>A result object containing the outcome of the operation and any generated sub-events.</returns>
        DomainOperationResult Commit(GameContext context);
    }

    /// <summary>
    /// A marker interface for game events that represent the core of a player's direct,
    /// cancellable action (e.g., playing a card, declaring an attack).
    /// Abilities triggered by events implementing this interface are subject to the
    /// pause-and-resume or cancellation flow if they require player input.
    /// </summary>
    public interface IPlayerActionEvent
    {
    }
}
