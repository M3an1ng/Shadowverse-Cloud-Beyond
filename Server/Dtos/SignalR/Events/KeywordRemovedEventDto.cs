// File: Server/Dtos/SignalR/Events/KeywordRemovedEventDto.cs
using System;
using ShadowverseBeyond.Server.Domain.Constants; // For CardKeyword enum

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// DTO for the event when a keyword is removed from a card.
    /// </summary>
    public class KeywordRemovedEventDto : GameEventDto
    {
        public Guid CardInstanceId { get; set; }

        /// <summary>
        /// The keyword that was removed (as a string).
        /// </summary>
        public string Keyword { get; set; } = string.Empty;

        // Constructor matching the base class and adding specific properties
        public KeywordRemovedEventDto(Guid sourcePlayerId, Guid cardInstanceId, CardKeyword keyword)
            : base(sourcePlayerId, "KeywordRemoved") // Event type string matches domain event logic
        {
            CardInstanceId = cardInstanceId;
            Keyword = keyword.ToString(); // Convert enum to string for DTO
        }
    }
}
