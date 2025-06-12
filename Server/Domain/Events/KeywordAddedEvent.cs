// File: Server/Domain/Events/KeywordAddedEvent.cs
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Constants; // For CardKeyword
using ShadowverseBeyond.Server.Dtos; // For PlayerStateDto
using AutoMapper; // Needed for constructor if mapping PlayerState
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Represents an event where a keyword is added to a card instance.
    /// </summary>
    public class KeywordAddedEvent : GameEventBase // Inherit from GameEventBase
    {
        /// <summary>
        /// The card instance that received the keyword.
        /// </summary>
        public CardInstance TargetCard { get; }

        /// <summary>
        /// The keyword that was added.
        /// </summary>
        public CardKeyword KeywordAdded { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeywordAddedEvent"/> class.
        /// </summary>
        /// <param name="targetCard">The card instance that received the keyword.</param>
        /// <param name="keywordAdded">The keyword that was added.</param>
        /// <param name="sourcePlayerState">The player state of the player whose action caused the event.</param>
        /// <param name="mapper">The AutoMapper instance to map PlayerState to PlayerStateDto.</param>
        public KeywordAddedEvent(CardInstance targetCard, CardKeyword keywordAdded, PlayerState sourcePlayerState, IMapper mapper)
            : base("keyword_added") // Use string event type
        {
            TargetCard = targetCard ?? throw new ArgumentNullException(nameof(targetCard));
            KeywordAdded = keywordAdded;

            // Set base properties
            SourceCard = targetCard; // The target card is the context
            SourcePlayer = mapper.Map<PlayerStateDto>(sourcePlayerState); // Map PlayerState to DTO for base property

            // Populate Parameters dictionary
            Parameters["targetCardInstanceId"] = targetCard.InstanceId;
            Parameters["targetCardDefinitionId"] = targetCard.DefinitionId;
            Parameters["keyword"] = keywordAdded.ToString();
            Parameters["sourcePlayerId"] = sourcePlayerState.PlayerId;
        }

        /// <summary>
        /// This event typically doesn't execute further logic itself.
        /// </summary>
        public override Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            return Task.FromResult(new List<IGameEvent>());
        }

        // Removed ToDto method, conversion should happen elsewhere or via AutoMapper profiles
    }
}