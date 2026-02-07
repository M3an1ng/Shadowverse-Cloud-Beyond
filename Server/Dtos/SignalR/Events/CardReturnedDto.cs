using System;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event broadcast when a card is returned to its owner's hand.
    /// </summary>
    public class CardReturnedDto : GameEventDto
    {
        public CardReturnedDto(
            Guid sourcePlayerId,
            Guid playerId,
            Guid cardUid,
            CardInstanceDto? returnedCard,
            string reason = "Effect")
            : base(sourcePlayerId, "Return")
        {
            PlayerId = playerId;
            CardUid = cardUid;
            ReturnedCard = returnedCard;
            Reason = reason ?? "Effect";
        }

        /// <summary>
        /// Owner of the returned card.
        /// </summary>
        public Guid PlayerId { get; set; } = Guid.Empty;

        /// <summary>
        /// UID of the returned card instance.
        /// </summary>
        public Guid CardUid { get; set; } = Guid.Empty;

        /// <summary>
        /// Snapshot of the returned card (optional, may be null if unavailable).
        /// </summary>
        public CardInstanceDto? ReturnedCard { get; set; }

        /// <summary>
        /// Return reason (e.g. Effect / ReturnEffect).
        /// </summary>
        public string Reason { get; set; } = "Effect";
    }
}
