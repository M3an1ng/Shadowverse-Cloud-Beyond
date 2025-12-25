using System;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// A trigger event that represents a successful Act activation for a specific card instance.
    /// Abilities can listen to <see cref="EventType.Act"/> and use relation filters (e.g. SourceSelf) to scope to the card.
    /// </summary>
    [GameEventMetadata(
        "启动",
        "当一张卡牌成功发动启动能力时触发的事件。",
        EventType.Act,
        AbilityEditorCategories.BaseActionId,
        AbilityEditorCategories.BaseActionLabel)]
    public sealed class ActInvokedEvent : GameEventBase
    {
        [GameEventParameter("发起玩家ID", "发动启动能力的玩家ID。")]
        public Guid RequestingPlayerId { get; }

        public ActInvokedEvent(CardInstance sourceCard, Guid requestingPlayerId)
            : base(EventType.Act)
        {
            Source = TargetInfo.ForCard(sourceCard);
            RequestingPlayerId = requestingPlayerId;
        }
    }
}
