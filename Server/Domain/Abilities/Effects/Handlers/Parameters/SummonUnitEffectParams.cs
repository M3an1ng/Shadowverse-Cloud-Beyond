// File: Server/Domain/Abilities/Effects/Handlers/Parameters/SummonUnitEffectParams.cs
using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Dtos; // For CardZone

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters specific to the SummonUnit effect.
    /// Defines which card to summon and how many.
    /// </summary>
    public class SummonUnitEffectParams : BaseEffectParams
    {
        /// <summary>
        /// The Definition ID of the card to summon.
        /// This is mandatory for summoning.
        /// </summary>
        public int CardDefinitionId { get; set; }

        /// <summary>
        /// The number of units to summon. Defaults to 1.
        /// </summary>
        public int SummonCount { get; set; } = 1;

        /// <summary>
        /// Optional: Specifies the zone to summon the unit to. Defaults to Field.
        /// Currently, only Field is typically supported for SummonUnit.
        /// </summary>
        public CardZone TargetZone { get; set; } = CardZone.Field;

        // Potential future parameters:
        // public bool SummonEvolved { get; set; } = false; // Summon the evolved version directly?
        // public Dictionary<string, object> InitialOverrides { get; set; } // Override stats/keywords on summon?
    }
}