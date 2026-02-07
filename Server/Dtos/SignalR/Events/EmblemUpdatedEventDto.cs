using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that emblem cards were added to a player's emblem zone.
    /// </summary>
    public class EmblemUpdatedEventDto : GameEventDto
    {
        /// <summary>
        /// The ID of the player who received the emblems.
        /// </summary>
        public Guid PlayerId { get; set; }

        /// <summary>
        /// The list of emblem card instances that were added.
        /// </summary>
        public List<CardInstanceDto> AddedEmblems { get; set; } = new();

        public EmblemUpdatedEventDto(Guid playerId, List<CardInstanceDto> addedEmblems)
            : base(playerId, "EmblemUpdated")
        {
            PlayerId = playerId;
            AddedEmblems = addedEmblems ?? new List<CardInstanceDto>();
        }
    }
}
