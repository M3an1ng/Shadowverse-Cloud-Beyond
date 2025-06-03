using ShadowverseBeyond.Server.Dtos.SignalR;
using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event DTO broadcast when a card ability is triggered.
    /// This allows other abilities to respond to specific ability tags being triggered.
    /// </summary>
    public class AbilityTriggeredEventDto : GameEventDto
    {
        /// <summary>
        /// The instance ID of the card that triggered the ability.
        /// </summary>
        public string SourceCardUid { get; set; } = string.Empty;

        /// <summary>
        /// The tag/type of the ability that was triggered (e.g., "Fanfare", "LastWords", "EvolveEffect").
        /// </summary>
        public string AbilityTag { get; set; } = string.Empty;

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
        /// <param name="abilityTag">The tag/type of the ability that was triggered.</param>
        /// <param name="triggerEventType">The type of event that triggered this ability.</param>
        /// <param name="abilityParameters">Additional parameters related to the ability trigger.</param>
        public AbilityTriggeredEventDto(
            string sourcePlayerId,
            string sourceCardUid,
            string abilityTag,
            string triggerEventType,
            Dictionary<string, object>? abilityParameters = null)
            : base(sourcePlayerId, "AbilityTriggered")
        {
            SourceCardUid = sourceCardUid ?? throw new ArgumentNullException(nameof(sourceCardUid));
            AbilityTag = abilityTag ?? throw new ArgumentNullException(nameof(abilityTag));
            TriggerEventType = triggerEventType ?? throw new ArgumentNullException(nameof(triggerEventType));
            AbilityParameters = abilityParameters ?? new Dictionary<string, object>();
        }
    }
}
