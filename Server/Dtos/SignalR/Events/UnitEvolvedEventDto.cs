using ShadowverseBeyond.Server.Dtos;         // For CardInstanceDto
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
        public UnitEvolvedEventDto(Guid sourcePlayerId, CardInstanceDto evolvedUnitState)
            : base(sourcePlayerId, "UnitEvolved") // Call base constructor
        {
            // Initialize required properties
            // EvolvingUnitUid is available within EvolvedUnitState.InstanceId
            EvolvedUnitState = evolvedUnitState;
        }

        // EvolvingUnitUid removed as it's redundant (present in EvolvedUnitState.InstanceId)
        /// <summary>
        /// The complete instance data of the unit after evolution.
        /// Includes updated stats, keywords, and potentially other state changes. The InstanceId identifies the evolved unit.
        /// </summary>
        public required CardInstanceDto EvolvedUnitState { get; set; }
    }
}