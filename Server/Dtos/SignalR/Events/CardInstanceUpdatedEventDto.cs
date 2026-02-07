using System;
using System.Diagnostics.CodeAnalysis;
using ShadowverseBeyond.Server.Dtos; // For CardInstanceDto

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that one or more card instances have updated snapshots.
    /// </summary>
    public class CardInstanceUpdatedEventDto : GameEventDto
    {
        /// <summary>
        /// Updated card instance snapshots.
        /// </summary>
        public List<CardInstanceDto> UpdatedCards { get; init; } = new List<CardInstanceDto>();

        [SetsRequiredMembers]
        public CardInstanceUpdatedEventDto(Guid sourcePlayerId, List<CardInstanceDto> updatedCards)
            : base(sourcePlayerId, "CardInstanceUpdated")
        {
            UpdatedCards = updatedCards ?? new List<CardInstanceDto>();
        }
    }
}
