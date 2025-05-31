// Server/Dtos/SignalR/Events/PlayerMulliganCompletedEventDto.cs
namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// DTO for the event indicating a player has completed their mulligan.
    /// Inherits from GameEventDto.
    /// </summary>
    public class PlayerMulliganCompletedEventDto : GameEventDto // Inherit from GameEventDto
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerMulliganCompletedEventDto"/> class.
        /// </summary>
        /// <param name="playerId">The ID of the player who completed the mulligan.</param>
        public PlayerMulliganCompletedEventDto(string playerId)
            : base(playerId, "PlayerMulliganCompleted")
        {
            // No specific properties to initialize for this event beyond the base class.
        }
    }
}