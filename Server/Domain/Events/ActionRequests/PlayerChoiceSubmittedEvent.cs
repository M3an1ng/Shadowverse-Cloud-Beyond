using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using System;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events.ActionRequests
{
    /// <summary>
    /// An internal domain event that represents a player's response to a choice request.
    /// This event is used to resume a paused transaction. It is not a player action itself,
    /// but rather the data required to continue a pending one.
    /// </summary>
    [GameEventMetadata(
        "玩家选择提交",
        "当玩家提交选择结果时触发的事件。",
        EventType.PlayerChoiceSubmitted,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public class PlayerChoiceSubmittedEvent : GameEventBase
    {
        [GameEventParameter("选择结果", "玩家提交的选择结果。")]
        public SubmitTargetChoiceActionDto Choice { get; }

        public PlayerChoiceSubmittedEvent(PlayerState requestingPlayer, SubmitTargetChoiceActionDto choice, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.PlayerChoiceSubmitted, eventId, timestamp)
        {
            // The source of this event is the player who made the choice.
            Source = TargetInfo.ForPlayer(requestingPlayer);
            Choice = choice;
        }
    }
}
