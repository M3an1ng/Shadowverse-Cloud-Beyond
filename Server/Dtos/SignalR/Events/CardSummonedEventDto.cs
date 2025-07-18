using System; // Added for ArgumentNullException
using ShadowverseBeyond.Server.Dtos.Enums; // For CardInstanceDto, GameEventDto base, CardZone

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event broadcast when a card instance enters the play area (field).
    /// This covers cards played from hand and units/amulets summoned by effects.
    /// </summary>
    public class CardSummonedEventDto : GameEventDto
    {
        /// <summary>
        /// The card instance that was summoned to the field.
        /// Contains all necessary details like InstanceId, CardId, Type, current stats, etc.
        /// </summary>
        public CardInstanceDto SummonedCard { get; init; }

        /// <summary>
        /// Indicates how the entity entered play (e.g., "Played", "Summoned", "Transformed").
        /// Useful for client-side animations or logic.
        /// </summary>
        public string Source { get; set; } = "Unknown"; // Consider using an enum later if needed

        /// <summary>
        /// Initializes a new instance of the <see cref="CardSummonedEventDto"/> class.
        /// </summary>
        /// <param name="sourcePlayerId">The ID of the player who caused the card to be summoned.</param>
        /// <param name="summonedCard">The card instance that was summoned.</param>
        /// <param name="source">Indicates how the card was summoned (e.g., "Played", "Summoned").</param>
        public CardSummonedEventDto(Guid sourcePlayerId, CardInstanceDto summonedCard, string source)
            : base(sourcePlayerId, nameof(CardSummonedEventDto))
        {
            SummonedCard = summonedCard ?? throw new ArgumentNullException(nameof(summonedCard));
            Source = source;
            // Ensure the summoned card's zone is Field, might be redundant if set by caller service
            if (SummonedCard.Zone != CardZone.Field)
            {
                // Log warning or adjust? For now, assume caller sets it correctly.
                // Consider throwing if strict validation is needed.
                // SummonedCard.Zone = CardZone.Field; // Avoid modifying DTO state here if possible
            }
        }
    }
}