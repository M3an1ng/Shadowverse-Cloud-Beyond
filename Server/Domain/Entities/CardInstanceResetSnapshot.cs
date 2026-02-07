using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Entities
{
    /// <summary>
    /// Lightweight, read-only snapshot of a card instance captured before reset.
    /// </summary>
    [DslExposable("卡牌重置前快照", Description = "卡牌在重置前保存的轻量快照")]
    public sealed record CardInstanceResetSnapshot
    {
        [DslExposable("卡牌ID", Description = "卡牌实例唯一标识")]
        public Guid InstanceId { get; init; }

        [DslExposable("拥有者ID", Description = "拥有者玩家ID")]
        public Guid OwnerPlayerId { get; init; }

        [DslExposable("基础定义ID", Description = "卡牌基础定义ID")]
        public int BaseDefinitionId { get; init; }

        [DslExposable("当前定义ID", Description = "当前卡牌定义ID")]
        public int CurrentDefinitionId { get; init; }

        [DslExposable("卡牌名称", Description = "卡牌名称")]
        public string Name { get; init; } = string.Empty;

        [DslExposable("卡牌类型", Description = "卡牌类型")]
        public CardType CardType { get; init; }

        [DslExposable("当前区域", Description = "卡牌当前区域")]
        public CardZone CurrentZone { get; init; }

        [DslExposable("上次移动原因", Description = "卡牌最近一次移动的原因")]
        public CardMoveReason LastMoveReason { get; init; }

        [DslExposable("当前攻击力", Description = "卡牌当前攻击力")]
        public int CurrentAttack { get; init; }

        [DslExposable("当前生命值", Description = "卡牌当前生命值")]
        public int CurrentHealth { get; init; }

        [DslExposable("最大生命值", Description = "卡牌最大生命值")]
        public int MaxHealth { get; init; }

        [DslExposable("当前费用", Description = "卡牌当前费用")]
        public int CurrentCost { get; init; }

        [DslExposable("是否已进化", Description = "是否已进化")]
        public bool IsEvolved { get; init; }

        [DslExposable("是否已超进化", Description = "是否已超进化")]
        public bool IsSuperEvolved { get; init; }

        [DslExposable("是否已沉默", Description = "是否已沉默")]
        public bool IsSilenced { get; init; }

        [DslExposable("关键字", Description = "当前关键字列表")]
        public IReadOnlyList<CardKeyword> Keywords { get; init; } = Array.Empty<CardKeyword>();

        [DslExposable("效果关键字", Description = "能力关键字列表")]
        public IReadOnlyList<AbilityKeyword> AbilityKeywords { get; init; } = Array.Empty<AbilityKeyword>();

        [DslExposable("倒计时", Description = "护符与纹章的倒计时值")]
        public int? Countdown { get; init; }

        [DslExposable("卡牌位置", Description = "卡牌在所在区域的位置排序")]
        public int? ZoneIndex { get; init; }

        [DslExposable("战场生存时间", Description = "卡牌在战场上存活了多少轮")]
        public int FieldTurn { get; init; }

        [DslExposable("可以攻击", Description = "该卡牌本回合是否可以攻击")]
        public bool CanAttack { get; init; }

        [DslExposable("当前攻击次数", Description = "该卡牌在回合中攻击的次数")]
        public int AttacksThisTurn { get; init; }

        [DslExposable("最大攻击次数", Description = "该卡牌每回合可攻击次数上限")]
        public int MaxAttacksPerTurn { get; init; }

        [DslExposable("变量", Description = "卡牌上的系统与临时计数器")]
        public IReadOnlyDictionary<string, int> CounterMap { get; init; } = new Dictionary<string, int>();

        [DslExposable("魔力增幅", Description = "spellboost 计数器数值")]
        public int Spellboost { get; init; }

        [DslExposable("土之印", Description = "earthsigil 计数器数值")]
        public int Earthsigil { get; init; }

        [DslExposable("卡名列表", Description = "卡牌可被视为的名称列表（包含基础名称）")]
        public IReadOnlyList<string> CardNames { get; init; } = Array.Empty<string>();

        [DslExposable("卡牌种类", Description = "卡牌所属的种类")]
        public IReadOnlyList<string> Tribe { get; init; } = Array.Empty<string>();

        public static CardInstanceResetSnapshot From(CardInstance card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));

            return new CardInstanceResetSnapshot
            {
                InstanceId = card.InstanceId,
                OwnerPlayerId = card.OwnerPlayerId,
                BaseDefinitionId = card.BaseDefinitionId,
                CurrentDefinitionId = card.CurrentDefinitionId,
                Name = card.Name,
                CardType = card.CardType,
                CurrentZone = card.CurrentZone,
                LastMoveReason = card.LastMoveReason,
                CurrentAttack = card.CurrentAttack,
                CurrentHealth = card.CurrentHealth,
                MaxHealth = card.MaxHealth,
                CurrentCost = card.CurrentCost,
                IsEvolved = card.IsEvolved,
                IsSuperEvolved = card.IsSuperEvolved,
                IsSilenced = card.IsSilenced,
                Keywords = card.Keywords?.ToList() ?? new List<CardKeyword>(),
                AbilityKeywords = card.AbilityKeywords?.ToList() ?? new List<AbilityKeyword>(),
                Countdown = card.Countdown,
                ZoneIndex = card.ZoneIndex,
                FieldTurn = card.FieldTurn,
                CanAttack = card.CanAttack,
                AttacksThisTurn = card.AttacksThisTurn,
                MaxAttacksPerTurn = card.MaxAttacksPerTurn,
                CounterMap = new Dictionary<string, int>(card.CounterMap ?? new Dictionary<string, int>()),
                Spellboost = card.Spellboost,
                Earthsigil = card.Earthsigil,
                CardNames = card.CardNames?.ToList() ?? new List<string>(),
                Tribe = card.Tribe?.ToList() ?? new List<string>()
            };
        }
    }
}
