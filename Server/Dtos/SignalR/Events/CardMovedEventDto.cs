using System;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating a card moved between zones.
    /// Frontend should treat this as the single source of truth for zone movement.
    /// </summary>
    public class CardMovedEventDto : GameEventDto
    {
        public CardMovedEventDto(
            Guid sourcePlayerId,
            Guid playerId,
            Guid cardUid,
            CardZone fromZone,
            CardZone toZone,
            string moveReason,
            CardInstanceDto? movedCard = null)
            : base(sourcePlayerId, "CardMoved")
        {
            PlayerId = playerId;
            CardUid = cardUid;
            FromZone = fromZone;
            ToZone = toZone;
            MoveReason = moveReason ?? "Unknown";
            MovedCard = movedCard;
        }

        /// <summary>
        /// Owner of the moved card.
        /// </summary>
        public Guid PlayerId { get; set; } = Guid.Empty;

        /// <summary>
        /// UID of the moved card instance.
        /// </summary>
        public Guid CardUid { get; set; } = Guid.Empty;

        /// <summary>
        /// Previous zone.
        /// </summary>
        public CardZone FromZone { get; set; } = CardZone.None;

        /// <summary>
        /// Destination zone.
        /// </summary>
        public CardZone ToZone { get; set; } = CardZone.None;

        /// <summary>
        /// Move reason (enum name string).
        /// </summary>
        public string MoveReason { get; set; } = "Unknown";

        /// <summary>
        /// Optional snapshot of moved card for hydration.
        /// </summary>
        public CardInstanceDto? MovedCard { get; set; }
    }
}
