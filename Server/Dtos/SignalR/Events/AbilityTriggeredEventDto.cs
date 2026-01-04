using ShadowverseBeyond.Server.Dtos.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event DTO broadcast when a card ability is triggered.
    /// This allows other abilities to respond to specific ability keywords being triggered.
    /// </summary>
    public class AbilityTriggeredEventDto : GameEventDto
    {
        /// <summary>
        /// The instance ID of the card that triggered the ability.
        /// </summary>
        public Guid SourceCardUid { get; set; } = Guid.Empty;

        /// <summary>
        /// The full keyword list for the triggered ability.
        /// </summary>
        public List<string> AbilityKeywords { get; set; } = new();

        /// <summary>
        /// The type of event that triggered this ability (e.g., "play_card", "summon", "card_destroyed").
        /// </summary>
        public string TriggerEventType { get; set; } = string.Empty;

        /// <summary>
        /// Additional parameters related to the ability trigger.
        /// </summary>
        public Dictionary<string, object> AbilityParameters { get; set; } = new();

        /// <summary>
        /// Initializes a new instance of the AbilityTriggeredEventDto class.
        /// </summary>
        /// <param name="sourcePlayerId">The ID of the player who controls the card with the triggered ability.</param>
        /// <param name="sourceCardUid">The instance ID of the card that triggered the ability.</param>
        /// <param name="triggerEventType">The type of event that triggered this ability.</param>
        /// <param name="abilityParameters">Additional parameters related to the ability trigger.</param>
        /// <param name="abilityKeywords">The keyword list for the triggered ability.</param>
        public AbilityTriggeredEventDto(
            Guid sourcePlayerId,
            Guid sourceCardUid,
            string triggerEventType,
            Dictionary<string, object>? abilityParameters = null,
            IReadOnlyList<string>? abilityKeywords = null)
            : base(sourcePlayerId, "AbilityTriggered")
        {
            SourceCardUid = sourceCardUid;
            TriggerEventType = triggerEventType ?? throw new ArgumentNullException(nameof(triggerEventType));
            AbilityParameters = abilityParameters ?? new Dictionary<string, object>();
            AbilityKeywords = abilityKeywords?.ToList() ?? new List<string>();
        }
    }
}
