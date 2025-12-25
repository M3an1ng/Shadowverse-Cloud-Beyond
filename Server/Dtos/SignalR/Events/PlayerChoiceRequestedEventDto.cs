using System;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public class PlayerChoiceRequestedEventDto : GameEventDto
    {
        public PlayerChoiceRequest ChoiceRequest { get; }

        public PlayerChoiceRequestedEventDto(Guid playerId, PlayerChoiceRequest choiceRequest)
            : base(playerId, "PlayerChoiceRequested")
        {
            ChoiceRequest = choiceRequest;
        }
    }
}