using System;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public class PlayerChoiceRequestedEventDto : GameEventDto
    {
        public PlayerChoiceRequest ChoiceRequest { get; }
        public Guid? ActionId { get; }
        public string? CommandId { get; }

        public PlayerChoiceRequestedEventDto(Guid playerId, PlayerChoiceRequest choiceRequest, Guid? actionId = null, string? commandId = null)
            : base(playerId, "PlayerChoiceRequested")
        {
            ChoiceRequest = choiceRequest;
            ActionId = actionId;
            CommandId = commandId;
        }
    }
}
