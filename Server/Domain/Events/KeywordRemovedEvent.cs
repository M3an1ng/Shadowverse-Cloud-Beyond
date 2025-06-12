// File: Server/Domain/Events/KeywordRemovedEvent.cs
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
    /// Represents an event where a keyword is removed from a card instance.
    /// </summary>
    public class KeywordRemovedEvent : GameEventBase // Inherit from GameEventBase
    {
        /// <summary>
        /// The card instance from which the keyword was removed.
        /// </summary>
        public CardInstance TargetCard { get; }

        /// <summary>
        /// The keyword that was removed.
        /// </summary>
        public CardKeyword KeywordRemoved { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeywordRemovedEvent"/> class.
        /// </summary>
        /// <param name="targetCard">The card instance from which the keyword was removed.</param>
        /// <param name="keywordRemoved">The keyword that was removed.</param>
        /// <param name="sourcePlayerState">The player state of the player whose action caused the event.</param>
        /// <param name="mapper">The AutoMapper instance to map PlayerState to PlayerStateDto.</param>
        public KeywordRemovedEvent(CardInstance targetCard, CardKeyword keywordRemoved, PlayerState sourcePlayerState, IMapper mapper)
            : base("keyword_removed") // Use string event type
        {
            TargetCard = targetCard ?? throw new ArgumentNullException(nameof(targetCard));
            KeywordRemoved = keywordRemoved;

            // Set base properties
            SourceCard = targetCard; // The target card is the context
            SourcePlayer = mapper.Map<PlayerStateDto>(sourcePlayerState); // Map PlayerState to DTO for base property

            // Populate Parameters dictionary
            Parameters["targetCardInstanceId"] = targetCard.InstanceId;
            Parameters["targetCardDefinitionId"] = targetCard.DefinitionId;
            Parameters["keyword"] = keywordRemoved.ToString();
            Parameters["sourcePlayerId"] = sourcePlayerState.PlayerId;
        }

        /// <summary>
        /// This event typically doesn't execute further logic itself.
        /// </summary>
        public override Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            return Task.FromResult(new List<IGameEvent>());
        }
    }
}