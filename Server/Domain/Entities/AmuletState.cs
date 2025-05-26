// File: Server/Domain/Entities/AmuletState.cs
namespace ShadowverseBeyond.Server.Domain.Entities
{
    /// <summary>
    /// Represents the state of an Amulet instance on the board.
    /// </summary>
    public class AmuletState : BoardEntityState
    {
        // Amulet-specific properties
        public int? Countdown { get; set; } // Nullable if the amulet doesn't have a countdown

        // Constructor
        public AmuletState(string instanceId, string ownerPlayerId, int definitionId, int? countdown = null)
            : base(instanceId, ownerPlayerId, definitionId)
        {
            Countdown = countdown;
        }

        // Parameterless constructor for serialization/ORM
        // Make it private or protected if direct instantiation isn't desired outside specific contexts (like ORM or factory)
        protected AmuletState() : base() { }

        // Amulet-specific methods (e.g., tick countdown)
        public void TickCountdown()
        {
            if (Countdown.HasValue)
            {
                Countdown--;
                // TODO: Add logic for reaching 0 (destruction, triggering Last Words etc.)
                // This might involve raising domain events for GameService to handle.
                if (Countdown <= 0)
                {
                    // Placeholder: Signal destruction or trigger event
                    // Example: RaiseDomainEvent(new AmuletCountdownFinishedEvent(this.InstanceId));
                }
            }
        }
    }
}