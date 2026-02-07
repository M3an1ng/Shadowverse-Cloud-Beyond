using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Immutable snapshot of a card definition for DSL evaluation.
    /// </summary>
    [DslExposable("原始卡牌", Description = "卡牌的原型，包含卡牌在无附加效果情况下的基础属性和定义")]
    public sealed record CardDefinitionSnapshot
    {
        [DslExposable("卡牌数据ID", Description = "卡牌数据的唯一标识符")]
        public int Id { get; init; }

        [DslExposable("名称", Description = "卡牌名称")]
        public string Name { get; init; } = string.Empty;

        [DslExposable("类型", Description = "卡牌类型")]
        public CardType Type { get; init; }

        [DslExposable("稀有度", Description = "卡牌稀有度")]
        public CardRarity Rarity { get; init; }

        [DslExposable("卡包", Description = "卡牌所属卡包")]
        public string CardSet { get; init; } = string.Empty;

        [DslExposable("原始费用", Description = "卡牌的基础费用")]
        public int Cost { get; init; }

        [DslExposable("原始攻击力", Description = "卡牌的基础攻击力")]
        public int? Attack { get; init; }

        [DslExposable("原始生命值", Description = "卡牌的基础生命值")]
        public int? Health { get; init; }

        [DslExposable("原始倒数", Description = "卡牌的基础倒数")]
        public int? Countdown { get; init; }

        [DslExposable("原始关键字", Description = "卡牌的基础关键字")]
        public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();

        [DslExposable("卡牌描述", Description = "卡牌的描述")]
        public string Description { get; init; } = string.Empty;

        [DslExposable("是否可收集", Description = "不可收集代表该卡牌为衍生物")]
        public bool IsCollectible { get; init; }

        [DslExposable("职业", Description = "卡牌所属职业")]
        public CardClass Class { get; init; }

        public static CardDefinitionSnapshot? From(CardDefinition? definition)
        {
            if (definition == null)
            {
                return null;
            }

            return new CardDefinitionSnapshot
            {
                Id = definition.Id,
                Name = definition.Name,
                Type = definition.Type,
                Rarity = definition.Rarity,
                CardSet = definition.CardSet ?? string.Empty,
                Cost = definition.Cost,
                Attack = definition.Attack,
                Health = definition.Health,
                Countdown = definition.Countdown,
                Keywords = definition.Keywords?.ToList() ?? new List<string>(),
                Description = definition.Description ?? string.Empty,
                IsCollectible = definition.IsCollectible,
                Class = definition.Class
            };
        }
    }
}
