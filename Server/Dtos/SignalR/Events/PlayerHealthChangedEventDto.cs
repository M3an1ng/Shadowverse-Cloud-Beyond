using ShadowverseBeyond.Server.Dtos.SignalR.Events; // Ensure base class namespace is used if needed

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating a player's (leader's) health has changed.
    /// Typically triggered after damage is dealt to a player.
    /// </summary>
    public class PlayerHealthChangedEventDto : GameEventDto // Inherit from base event DTO
    {
        /// <summary>
        /// Gets the type of the game event.
        /// </summary>
        public override string Type => "PlayerHealthChanged";
        /// <summary>
        /// The identifier of the player whose health changed.
        /// </summary>
        public string PlayerId { get; set; } = string.Empty;

        /// <summary>
        /// The player's health value *after* the change.
        /// </summary>
        public int NewHealth { get; set; }

        /// <summary>
        /// The amount by which the player's health changed (e.g., -5 for 5 damage).
        /// This helps the client potentially show damage numbers or healing effects.
        /// </summary>
        public int ChangeAmount { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerHealthChangedEventDto"/> class.
        /// Sets the event type for client-side discrimination.
        /// </summary>
        public PlayerHealthChangedEventDto()
        {
            // The event type is handled by the override Type property.
            // Remove the incorrect assignment to the non-existent EventType property.
        }
    }
}