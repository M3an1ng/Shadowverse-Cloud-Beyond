// File: Server/Domain/Abilities/Effects/Handlers/Parameters/DrawCardEffectParams.cs
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities; // For PlayerState if needed later

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the DrawCard effect.
    /// </summary>
    public class DrawCardEffectParams : BaseEffectParams
    {
        /// <summary>
        /// The target(s) of the draw effect. This should resolve to one or more players.
        /// </summary>

        /// <summary>
        /// The number of cards to draw.
        /// Can be a static value or resolved from a DSL expression.
        /// Defaults to 1.
        /// </summary>
        public int DrawAmount { get; set; } = 1;

        // Potential future parameters:
        // public string SourceZone { get; set; } = "Deck"; // e.g., Draw from Graveyard
        // public CardFilter DrawFilter { get; set; } // e.g., Draw a Follower
    }
}