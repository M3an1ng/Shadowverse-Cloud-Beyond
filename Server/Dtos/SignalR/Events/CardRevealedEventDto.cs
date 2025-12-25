using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using System;
using System.Diagnostics.CodeAnalysis;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public sealed class CardRevealedEventDto : GameEventDto
    {
        public Guid RevealingPlayerId { get; init; }
        public Guid RevealedCardInstanceId { get; init; }
        public CardInstanceDto RevealedCard { get; init; } = new();

        [SetsRequiredMembers]
        public CardRevealedEventDto(Guid sourcePlayerId, Guid revealingPlayerId, Guid revealedCardInstanceId, CardInstanceDto revealedCard)
            : base(sourcePlayerId, "CardRevealed")
        {
            RevealingPlayerId = revealingPlayerId;
            RevealedCardInstanceId = revealedCardInstanceId;
            RevealedCard = revealedCard ?? new CardInstanceDto();
        }
    }
}

