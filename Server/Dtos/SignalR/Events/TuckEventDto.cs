using System;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event broadcast when a card is tucked into deck.
    /// This DTO is reserved for visual effects and logs; zone movement is handled by CardMovedEventDto.
    /// </summary>
    public class TuckEventDto : GameEventDto
    {
        public TuckEventDto(
            Guid sourcePlayerId,
            Guid playerId,
            Guid cardUid,
            CardInstanceDto? tuckedCard,
            string reason = "Effect")
            : base(sourcePlayerId, "Tuck")
        {
            PlayerId = playerId;
            CardUid = cardUid;
            TuckedCard = tuckedCard;
            Reason = reason ?? "Effect";
        }

        public Guid PlayerId { get; set; } = Guid.Empty;

        public Guid CardUid { get; set; } = Guid.Empty;

        public CardInstanceDto? TuckedCard { get; set; }

        public string Reason { get; set; } = "Effect";
    }
}
