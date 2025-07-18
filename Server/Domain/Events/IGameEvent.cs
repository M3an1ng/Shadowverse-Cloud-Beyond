//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
//
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
        /// Additional parameters specific to this event type.
        /// </summary>
        Dictionary<string, object> Parameters { get; }
    }

    /// <summary>
    /// Base abstract class for game events providing common functionality.
    /// </summary>
    public abstract class GameEventBase : IGameEvent
    {
        public EventType EventType { get; protected set; }
        public Guid EventId { get; }
        public DateTime Timestamp { get; }
        public Dictionary<string, object> Parameters { get; set; }

        public TargetInfo? Source { get; protected set; }

        protected GameEventBase(EventType eventType)
        {
            EventType = eventType;
            EventId = Guid.NewGuid();
            Timestamp = DateTime.UtcNow;
            Parameters = new Dictionary<string, object>();
        }
    }
}
