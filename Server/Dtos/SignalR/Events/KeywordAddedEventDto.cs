// File: Server/Dtos/SignalR/Events/KeywordAddedEventDto.cs
using System;
using ShadowverseBeyond.Server.Domain.Constants; // For CardKeyword enum

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// DTO for the event when a keyword is added to a card.
    /// </summary>
    public class KeywordAddedEventDto : GameEventDto
    {

        /// <summary>
        /// The keyword that was added (as a string).
        /// </summary>
        public string Keyword { get; set; } = string.Empty;

        // Constructor matching the base class and adding specific properties
        public KeywordAddedEventDto(Guid sourcePlayerId, CardKeyword keyword)
            : base(sourcePlayerId, "KeywordAdded") // Event type string matches domain event logic
        {
            Keyword = keyword.ToString(); // Convert enum to string for DTO
        }
    }
}