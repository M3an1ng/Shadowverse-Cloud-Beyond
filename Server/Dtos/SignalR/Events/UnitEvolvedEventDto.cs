using ShadowverseBeyond.Server.Dtos;         // For UnitStateDto
using ShadowverseBeyond.Server.Dtos.SignalR; // For GameEventDto

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that a unit on the board has evolved.
    /// </summary>
    public class UnitEvolvedEventDto : GameEventDto
    {
        public override string Type => "UnitEvolved";

        /// <summary>
        /// The unique identifier of the unit that evolved.
        /// </summary>
        public required string EvolvingUnitUid { get; set; }

        /// <summary>
        /// The complete state of the unit after evolution.
        /// Includes updated stats, keywords, and the IsEvolved flag set to true.
        /// </summary>
        public required UnitStateDto EvolvedUnitState { get; set; }
    }
}