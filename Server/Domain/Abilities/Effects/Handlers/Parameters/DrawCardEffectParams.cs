// File: Server/Domain/Abilities/Effects/Handlers/Parameters/DrawCardEffectParams.cs
using ShadowverseBeyond.Server.Domain.Entities; // For PlayerState if needed later

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the DrawCard effect.
    /// </summary>
    public class DrawCardEffectParams : BaseTargetEffectParams // Target is usually the player
    {
        /// <summary>
        /// The number of cards to draw.
        /// Can be a static value or resolved from a DSL expression.
        /// Defaults to 1.
        /// </summary>
        public int DrawAmount { get; set; } = 1; // Renamed from DrawCount to DrawAmount

        // Potential future parameters:
        // public string SourceZone { get; set; } = "Deck"; // e.g., Draw from Graveyard
        // public CardFilter DrawFilter { get; set; } // e.g., Draw a Follower
    }
}