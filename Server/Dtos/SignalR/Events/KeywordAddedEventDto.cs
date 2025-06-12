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
        /// The InstanceId of the card that received the keyword.
        /// </summary>
        public Guid TargetCardInstanceId { get; set; }

        /// <summary>
        /// The DefinitionId of the card that received the keyword.
        /// </summary>
        public int TargetCardDefinitionId { get; set; }

        /// <summary>
        /// The keyword that was added (as a string).
        /// </summary>
        public string Keyword { get; set; } = string.Empty;

        // Constructor matching the base class and adding specific properties
        public KeywordAddedEventDto(Guid sourcePlayerId, Guid targetCardInstanceId, int targetCardDefinitionId, CardKeyword keyword)
            : base(sourcePlayerId, "KeywordAdded") // Event type string matches domain event logic
        {
            TargetCardInstanceId = targetCardInstanceId;
            TargetCardDefinitionId = targetCardDefinitionId;
            Keyword = keyword.ToString(); // Convert enum to string for DTO
        }
    }
}