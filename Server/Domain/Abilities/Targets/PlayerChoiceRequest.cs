using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets
{
    /// <summary>
    /// Represents a request sent to the client when the game needs the player to make a choice.
    /// </summary>
    public class PlayerChoiceRequest
    {
        /// <summary>
        /// A unique identifier for this choice request, used to resume the action later.
        /// </summary>
        public Guid PendingChoiceId { get; set; }

        /// <summary>
        /// The ID of the game this request belongs to.
        /// </summary>
        public string GameId { get; set; } = string.Empty;

        /// <summary>
        /// The key from the ability's Target configuration that this choice corresponds to.
        /// </summary>
        public string TargetKey { get; set; } = string.Empty;

        /// <summary>
        /// The runtime ID of the ability that triggered this choice request.
        /// </summary>
        public Guid SourceAbilityRuntimeId { get; set; }

        /// <summary>
        /// The ID of the player who needs to make the choice.
        /// </summary>
        public Guid RequestingPlayerId { get; set; }

        /// <summary>
        /// A message to display to the player (e.g., "Choose a target for your spell.").
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// The list of valid targets the player can choose from.
        /// </summary>
        public List<Guid> SelectableTargetIds { get; set; } = new();

        /// <summary>
        /// Optional renderable card data for selectable targets.
        /// This is used when selectable targets are not currently visible/clickable in the client's default UI
        /// (e.g., deck candidate list / graveyard selection panel).
        /// </summary>
        public List<CardInstanceDto> SelectableCardInstances { get; set; } = new();

        /// <summary>
        /// The minimum number of targets the player must select.
        /// </summary>
        public int MinSelectionCount { get; set; }

        /// <summary>
        /// The maximum number of targets the player can select.
        /// </summary>
        public int MaxSelectionCount { get; set; }

        /// <summary>
        /// Indicates whether this choice can be cancelled by the player.
        /// </summary>
        public bool IsCancellable { get; set; }
    }
}
