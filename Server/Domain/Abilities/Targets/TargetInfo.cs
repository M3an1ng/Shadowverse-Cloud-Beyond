using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;
using System;

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets
{
    /// <summary>
    /// Represents a resolved target in the game, containing more context than a simple ID.
    /// This replaces the old TargetId to provide richer information for effects and DSL evaluation.
    /// </summary>
    [DslExposable("目标信息", Description = "一个已解析的目标引用（玩家或卡牌），包含实体引用与基础标识信息")]
    public record TargetInfo
    {
        /// <summary>
        /// The unique identifier of the target.
        /// </summary>
        [DslExposable("目标ID", Description = "目标的唯一标识符（CardInstanceId 或 PlayerId）")]
        public Guid InstanceId { get; }

        /// <summary>
        /// The type of the target (e.g., Unit, Player).
        /// </summary>
        [DslExposable("目标类型", Description = "目标类型（Card / Player）")]
        public TargetType Type { get; }

        /// <summary>
        /// The CardInstance if the target is a card. Null otherwise.
        /// </summary>
        [DslExposable("卡牌", Description = "当目标为卡牌时，可访问对应的 CardInstance")]
        public CardInstance? Card { get; }

        /// <summary>
        /// The PlayerState if the target is a player. Null otherwise.
        /// </summary>
        [DslExposable("玩家", Description = "当目标为玩家时，可访问对应的 PlayerState")]
        public PlayerState? Player { get; }

        /// <summary>
        /// Gets the target as a CardInstance, if its type is Card. Otherwise, returns null.
        /// </summary>
        public CardInstance? AsCard => Type == TargetType.Card ? Card : null;

        /// <summary>
        /// Gets the target as a PlayerState, if its type is Player. Otherwise, returns null.
        /// </summary>
        public PlayerState? AsPlayer => Type == TargetType.Player ? Player : null;

        private TargetInfo(Guid id, TargetType type, CardInstance? card, PlayerState? player)
        {
            InstanceId = id;
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
