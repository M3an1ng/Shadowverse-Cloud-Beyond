using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that a card has vanished and been removed from the game.
    /// </summary>
    public class CardVanishedEventDto : GameEventDto
    {
        public CardVanishedEventDto(Guid sourcePlayerId, Guid cardUid, Guid playerId, string reason = "Unknown")
            : base(sourcePlayerId, "CardVanished")
        {
            CardUid = cardUid;
            PlayerId = playerId;
            Reason = reason;
        }

        /// <summary>
        /// The unique identifier (UID) of the vanished card.
        /// </summary>
        public Guid CardUid { get; set; } = Guid.Empty;

        /// <summary>
        /// The identifier of the player who owned the vanished card.
        /// </summary>
        public Guid PlayerId { get; set; } = Guid.Empty;

        /// <summary>
        /// Optional: Indicates the reason source for vanishing (e.g., "Ability", "System").
        /// </summary>
        public string Reason { get; set; } = "Unknown";
    }
}
