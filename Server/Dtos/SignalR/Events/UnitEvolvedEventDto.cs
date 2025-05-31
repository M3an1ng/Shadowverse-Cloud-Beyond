using ShadowverseBeyond.Server.Dtos;         // For UnitStateDto
using ShadowverseBeyond.Server.Dtos.SignalR; // For GameEventDto
using System.Diagnostics.CodeAnalysis; // For [SetsRequiredMembers]

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that a unit on the board has evolved.
    /// </summary>
    public class UnitEvolvedEventDto : GameEventDto
    {
        // Constructor
        [SetsRequiredMembers]
        public UnitEvolvedEventDto(string sourcePlayerId, string evolvingUnitUid, UnitStateDto evolvedUnitState)
            : base(sourcePlayerId, "UnitEvolved") // Call base constructor
        {
            // Initialize required properties
            EvolvingUnitUid = evolvingUnitUid;
            EvolvedUnitState = evolvedUnitState;
        }

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