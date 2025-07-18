using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Entities;
using System;

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets
{
    /// <summary>
    /// Represents a resolved target in the game, containing more context than a simple ID.
    /// This replaces the old TargetId to provide richer information for effects and DSL evaluation.
    /// </summary>
    public record TargetInfo
    {
        /// <summary>
        /// The unique identifier of the target.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// The type of the target (e.g., Unit, Player).
        /// </summary>
        public TargetType Type { get; }

        /// <summary>
        /// The CardInstance if the target is a card. Null otherwise.
        /// </summary>
        public CardInstance? Card { get; }

        /// <summary>
        /// The PlayerState if the target is a player. Null otherwise.
        /// </summary>
        public PlayerState? Player { get; }

        private TargetInfo(Guid id, TargetType type, CardInstance? card, PlayerState? player)
        {
            Id = id;
            Type = type;
            Card = card;
            Player = player;
        }

        /// <summary>
        /// Creates a TargetInfo for a card target.
        /// </summary>
        public static TargetInfo ForCard(CardInstance card)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));
            return new TargetInfo(card.InstanceId, TargetType.Card, card, null);
        }

        /// <summary>
        /// Creates a TargetInfo for a player target.
        /// </summary>
        public static TargetInfo ForPlayer(PlayerState player)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            return new TargetInfo(player.PlayerId, TargetType.Player, null, player);
        }
    }
}