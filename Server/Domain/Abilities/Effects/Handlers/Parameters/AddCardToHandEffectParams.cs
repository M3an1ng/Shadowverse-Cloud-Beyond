// using ShadowverseBeyond.Server.Domain.Abilities.Effects.Parameters; // No longer needed if base class is in the same new namespace or accessible

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters // Corrected namespace to match file location
{
    /// <summary>
    /// Parameters for the AddCardToHand effect.
    /// </summary>
    // Inherit from BaseEffectParams which contains common properties like GenerateBattleLog
    public class AddCardToHandEffectParams : BaseEffectParams // Changed base class to BaseEffectParams
    {
        /// <summary>
        /// The ID of the card definition to add to the hand.
        /// </summary>
        public int CardDefinitionId { get; set; }

        /// <summary>
        /// The number of cards to add. Defaults to 1.
        /// </summary>
        public int Amount { get; set; } = 1;
    }
}