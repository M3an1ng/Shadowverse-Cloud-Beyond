using ShadowverseBeyond.Server.Dtos; // For CardInstanceDto
using System.Text.Json.Serialization;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event broadcast when a player plays any card (Unit, Spell, Amulet) from their hand.
    /// This event signifies the action of playing, not necessarily the result 
    /// (e.g., a unit actually entering play is handled by EntityEnteredPlayEventDto).
    /// It's primarily for UI updates like removing the card from hand, playing animations, updating PP.
    /// </summary>
    public class CardPlayedEventDto : GameEventDto
    {
        /// <summary>
        /// Instance data of the card that was played from the hand.
        /// Contains the InstanceId (for removal from hand UI) and other basic info.
        /// </summary>
        public CardInstanceDto PlayedCardInstance { get; set; } = null!; // Assume instance is non-null

        // Note: Target information is intentionally omitted here for simplicity.
        // The effects or results of playing the card (like entity entering play, damage dealt, etc.)
        // will be communicated through other specific events (e.g., EntityEnteredPlayEventDto, DamageDealtEventDto).

        /// <summary>
        /// Initializes a new instance of the <see cref="CardPlayedEventDto"/> class.
        /// </summary>
        /// <param name="sourcePlayerId">The ID of the player who initiated the event (played the card).</param>
        /// <param name="playedCardInstance">Instance data for the card that was played from hand. Can be null if the instance data is somehow lost, though this indicates an error state.</param>
        public CardPlayedEventDto(string sourcePlayerId, CardInstanceDto? playedCardInstance) // Make parameter nullable
            : base(sourcePlayerId, nameof(CardPlayedEventDto))
        {
            // Null check remains valid
            PlayedCardInstance = playedCardInstance ?? throw new ArgumentNullException(nameof(playedCardInstance), "PlayedCardInstance cannot be null for CardPlayedEventDto.");
        }
    }
}