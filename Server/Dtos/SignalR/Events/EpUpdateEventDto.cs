using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating an update to a player's Evolution Points (EP).
    /// Typically sent at the start of specific turns or after evolving a unit.
    /// </summary>
    public class EpUpdateEventDto : GameEventDto
    {


        /// <summary>
        /// The player's current EP amount after the update.
        /// </summary>
        public int CurrentEp { get; set; }

        /// <summary>
        /// The player's maximum EP amount after the update (usually doesn't change mid-game, but included for completeness).
        /// </summary>
        public int MaxEp { get; set; }
        /// <summary>
        /// Initializes a new instance of the <see cref="EpUpdateEventDto"/> class.
        /// </summary>
        /// <param name="playerId">The identifier of the player whose EP was updated.</param>
        /// <param name="currentEp">The player's current EP amount after the update.</param>
        /// <param name="maxEp">The player's maximum EP amount after the update.</param>
        public EpUpdateEventDto(Guid playerId, int currentEp, int maxEp)
            : base(playerId, "EpUpdate") // Set base PlayerId and Type
        {
            this.CurrentEp = currentEp;
            this.MaxEp = maxEp;
        }
    }
}