using AutoMapper; // Added for IMapper
using System;
//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 战斗系统: 文档/05-游戏系统/02-战斗系统.md
// - 回合管理系统: 文档/05-游戏系统/03-回合管理系统.md
//
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums; // Add using for GamePhase
using ShadowverseBeyond.Server.Dtos.SignalR.Actions; // Added for action DTOs
using ShadowverseBeyond.Server.Models; // Add using for GameActionContext
using ShadowverseBeyond.Server.Services; // Add using for IAiService
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq; // Added for FirstOrDefault
using ShadowverseBeyond.Server.Domain.Buffs; // Add using for Buff
using ShadowverseBeyond.Server.Domain.Abilities; // Add using for Ability
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters; // Add using for AttackTargetType
using System; // Add using for ArgumentNullException
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime; // Add using for GetService
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;
using ShadowverseBeyond.Server.Domain.Events.Services;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShadowverseBeyond.Server.Domain.Events
{

    /// <summary>
    /// Event triggered when two units engage in combat (clash).
    /// This event is fired before damage calculation and allows abilities to trigger.
    /// </summary>
    [GameEventMetadata("交战", "当两个单位进行战斗时触发的事件。", EventType.Clash)]
    public class ClashEvent : GameEventBase
    {
        [GameEventParameter("攻击者", "发起攻击的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? Attacker => Source?.Card;

        [GameEventParameter("防御者", "被攻击的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? Defender => Target.FirstOrDefault()?.Card;

        public ClashEvent(CardInstance attacker, CardInstance defender, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Clash, eventId, timestamp)
        {
            Source = TargetInfo.ForCard(attacker);
            Target.Add(TargetInfo.ForCard(defender));
        }
    }
    /// <summary>
    /// Event triggered when a card instance's health changes.
    /// </summary>




    /// <summary>
    /// Event triggered when a player's hand is updated (e.g., after mulligan, drawing cards, etc.).
    /// This event notifies the frontend about changes to the hand state.
    /// </summary>
    [GameEventMetadata("手牌更新", "当玩家的手牌发生变化时触发的事件。", EventType.HandUpdated)]
    public class HandUpdatedEvent : GameEventBase
    {
        [GameEventParameter("更新后的手牌", "玩家更新后的手牌列表。")]
        public List<CardInstance> UpdatedHand { get; }

        [GameEventParameter("目标玩家", "手牌被更新的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? TargetPlayer => Source?.Player;

        public HandUpdatedEvent(PlayerState player, List<CardInstance> updatedHand, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.HandUpdated, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            UpdatedHand = updatedHand;
        }
    }

    // /// <summary>
    // /// Event triggered when an amulet's countdown reaches zero.
    // /// </summary>
    // [GameEventMetadata("倒计时结束", "当护符的倒计时归零时触发的事件。", EventType.CountdownFinished)]
    // public class CountdownFinishedEvent : GameEventBase
    // {
    //     [GameEventParameter("拥有者", "护符的拥有者玩家。")]
    //     public PlayerState? Owner => Source?.Player;

    //     [GameEventParameter("护符", "倒计时结束的护符。")]
    //     public CardInstance? Amulet => Target.FirstOrDefault()?.Card;

    //     public CountdownFinishedEvent(PlayerState player, CardInstance amulet, Guid? eventId = null, DateTime? timestamp = null)
    //         : base(EventType.CountdownFinished, eventId, timestamp)
    //     {
    //         Source = TargetInfo.ForPlayer(player);
    //         Target.Add(TargetInfo.ForCard(amulet));
    //     }
    // }





    /// <summary>
    /// Event triggered when silence is applied to a target.
    /// </summary>
    [GameEventMetadata("沉默应用", "当一张卡牌被沉默时触发的事件。", EventType.SilenceApplied, visibility: EventMetadataVisibility.Hidden)]
    public class SilenceAppliedEvent : GameEventBase
    {
        /// <summary>
        /// The source ability that applied the silence (if any).
        /// </summary>
        [GameEventParameter("来源能力", "应用沉默的能力。")]
        public Ability? SourceAbility { get; }

        [GameEventParameter("沉默来源", "应用沉默的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? SilenceSource => Source?.Card;

        [GameEventParameter("沉默目标", "被沉默的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? SilenceTarget => Target.FirstOrDefault()?.Card;

        public SilenceAppliedEvent(CardInstance target, CardInstance? sourceCard = null, Ability? sourceAbility = null, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.SilenceApplied, eventId, timestamp)
        {
            Target.Add(TargetInfo.ForCard(target ?? throw new ArgumentNullException(nameof(target))));
            SourceAbility = sourceAbility;
            if (sourceCard != null)
            {
                Source = TargetInfo.ForCard(sourceCard);
            }
        }

    }

    /// <summary>
    /// Event triggered when silence is removed from a target.
    /// </summary>
    [GameEventMetadata("沉默移除", "当一张卡牌的沉默效果被移除时触发的事件。", EventType.SilenceRemoved, visibility: EventMetadataVisibility.Hidden)]
    public class SilenceRemovedEvent : GameEventBase
    {
        [GameEventParameter("移除来源", "移除沉默的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? RemovalSource => Source?.Card;

        [GameEventParameter("移除目标", "沉默被移除的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? RemovalTarget => Target.FirstOrDefault()?.Card;

        public SilenceRemovedEvent(CardInstance target, CardInstance source, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.SilenceRemoved, eventId, timestamp)
        {
            Target.Add(TargetInfo.ForCard(target ?? throw new ArgumentNullException(nameof(target))));
            Source = TargetInfo.ForCard(source);
        }

    }
    /// <summary>
    /// Represents the reason why a card was discarded.
    /// </summary>
    public enum DiscardReason
    {
        /// <summary>Discarded due to a card effect.</summary>
        Effect,
        /// <summary>Discarded due to having a full hand when drawing.</summary>
        Overdraw,
        /// <summary>Discarded during the mulligan phase.</summary>
        Mulligan,
        /// <summary>Discarded for other or unknown reasons.</summary>
        Other
    }

    /// <summary>
    /// 原子事实事件：当玩家牌库数量发生变化时触发。
    /// </summary>
    public class DeckCountChangedEvent : GameEventBase
    {
        public int NewDeckCount { get; }

        public DeckCountChangedEvent(PlayerState player, int newDeckCount, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.DeckUpdated, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            NewDeckCount = newDeckCount;
        }
    }

    /// <summary>
    /// 原子事实事件：当卡牌因手牌已满而被爆牌时触发。
    /// </summary>
    [GameEventMetadata("爆牌", "当卡牌因手牌已满而被爆牌时触发。", EventType.CardBurned, visibility: EventMetadataVisibility.Visible)]
    public class CardBurnedEvent : GameEventBase
    {
        [GameEventParameter("爆牌玩家", "发生爆牌的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? BurnedPlayer => Source?.Player;

        [GameEventParameter("爆牌卡牌", "被爆牌的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? BurnedCard => Target.FirstOrDefault()?.Card;

        public CardBurnedEvent(PlayerState player, CardInstance card, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.CardBurned, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            Target.Add(TargetInfo.ForCard(card));
        }
    }

    /// <summary>
    /// 原子事实事件：当卡牌因战场已满而无法进入战场时触发。
    /// </summary>
    [GameEventMetadata("满场", "当卡牌因战场已满而无法进入战场时触发。", EventType.CardPacked, visibility: EventMetadataVisibility.Visible)]
    public class CardPackedEvent : GameEventBase
    {
        [GameEventParameter("满场玩家", "战场已满的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? PackedPlayer => Source?.Player;

        [GameEventParameter("满场卡牌", "因战场已满而无法进入战场的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? PackedCard => Target.FirstOrDefault()?.Card;

        public CardPackedEvent(PlayerState player, CardInstance card, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.CardPacked, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            Target.Add(TargetInfo.ForCard(card));
        }
    }

    /// <summary>
    /// 原子事实事件：当玩家试图抽牌但牌库为空时触发。
    /// </summary>
    [GameEventMetadata("抽空", "当玩家试图抽牌但牌库为空时触发。", EventType.DeckEmptyOnDraw, visibility: EventMetadataVisibility.Visible)]
    public class DeckEmptyOnDrawEvent : GameEventBase, ICommittableEvent
    {
        public DeckEmptyOnDrawEvent(PlayerState player, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.DeckEmptyOnDraw, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult { Success = true };

            if (context.CurrentGameState.IsGameOver)
            {
                return result;
            }

            var player = Source?.Player;
            if (player == null)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "DeckEmptyOnDrawEvent missing source player."
                };
            }

            var opponent = context.CurrentGameState.GetOpponentPlayerState(player.PlayerId);
            if (opponent == null)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"DeckEmptyOnDrawEvent failed to resolve opponent for player {player.PlayerId}."
                };
            }

            result.GeneratedEvents.Add(new GameEndEvent(opponent.PlayerId, player.PlayerId, "Deck out"));
            return result;
        }
    }

    /// <summary>
    /// 效果处理器聚合事实事件：当任何效果处理器完成其主要操作后触发。
    /// </summary>
    [GameEventMetadata("效果完成", "当效果执行完成后触发的事件。", EventType.EffectCompleted, visibility: EventMetadataVisibility.Hidden)]
    [DslExposable("效果完成事件", Description = "效果执行完成后产生的聚合事实事件，包含输入参数、目标与输出等信息")]
    public class EffectCompletedEvent : GameEventBase, IRawEventBackedGameEvent
    {
        /// <summary>
        /// 原始效果的类型字符串 (来自 EffectConfig.Type)。
        /// </summary>
        [GameEventParameter("效果类型", "该 EffectCompletedEvent 对应的效果类型")]
        [DslExposable("效果类型", Description = "该 EffectCompletedEvent 对应的效果类型")]
        public EffectType OriginalEffectType { get; }

        /// <summary>
        /// 原始效果的输入参数 (经过DSL初步解析和类型转换后的值)。
        /// </summary>
        [GameEventParameter("输入参数", "效果处理器的输入参数（解析后）")]
        [DslExposable("输入参数", Description = "效果处理器的输入参数（解析后）")]
        public IReadOnlyDictionary<string, object> OriginalEffectInputParams { get; }

        /// <summary>
        /// 效果执行前解析出的最终目标列表。
        /// </summary>
        [GameEventParameter("解析目标", "父效果执行时使用的目标列表")]
        [DslExposable("解析目标", Description = "父效果执行时使用的目标列表")]
        public IReadOnlyList<TargetInfo> ResolvedTargets { get; }

        /// <summary>
        /// 效果执行过程中生成的事件列表（不包含 EffectCompletedEvent 自身）。
        /// </summary>
        [GameEventParameter("生成事件", "效果执行过程中生成的事件列表。")]
        [DslExposable("生成事件", Description = "效果执行过程中生成的事件列表（不包含自身）")]
        public IReadOnlyList<IGameEvent> GeneratedEvents { get; }

        /// <summary>
        /// 效果执行过程中生成事件的目标卡牌集合。
        /// </summary>
        [GameEventParameter("上述卡牌", "效果执行过程中事件的目标卡牌列表。")]
        [DslExposable("上述卡牌", Description = "效果执行过程中事件的目标卡牌集合")]
        public IReadOnlyList<CardInstance> GeneratedCards { get; }

        /// <summary>
        /// 效果执行完成时刻生成卡牌的不可变快照。
        /// </summary>
        [GameEventParameter("上述卡牌（快照）", "效果执行完成时刻生成卡牌的不可变快照列表。")]
        [DslExposable("上述卡牌（快照）", Description = "效果执行完成时刻生成卡牌的不可变快照集合")]
        public IReadOnlyList<CardInstanceSnapshot> GeneratedCardSnapshots { get; }

        /// <summary>
        /// 根据 EffectConfig.Outputs 配置和DSL表达式计算得出的输出参数。
        /// </summary>
        [GameEventParameter("输出参数", "根据 Outputs DSL 计算出的输出字典")]
        [DslExposable("输出参数", Description = "根据 Outputs DSL 计算出的输出字典")]
        public IReadOnlyDictionary<string, object?> Outputs { get; }

        /// <summary>
        /// 父效果执行是否成功。
        /// </summary>
        [GameEventParameter("执行是否成功", "父效果执行是否成功。")]
        [DslExposable("执行是否成功", Description = "父效果执行是否成功")]
        public bool ParentEffectSucceeded { get; }

        public JsonObject OutputPayload { get; }

        // SourceCard (来自 GameEventBase): 产生此效果的源卡牌实例。


        public EffectCompletedEvent(
            EffectType originalEffectType,
            IReadOnlyDictionary<string, object> originalEffectInputParams,
            IReadOnlyList<TargetInfo> resolvedTargets,
            IReadOnlyList<IGameEvent> generatedEvents,
            IReadOnlyList<CardInstance> generatedCards,
            IReadOnlyList<CardInstanceSnapshot> generatedCardSnapshots,
            IReadOnlyDictionary<string, object?> outputs,
            bool parentEffectSucceeded,
            CardInstance? sourceCard,
            Guid? eventId = null,
            DateTime? timestamp = null
        ) : base(EventType.EffectCompleted, eventId, timestamp)
        {
            OriginalEffectType = originalEffectType;
            OriginalEffectInputParams = originalEffectInputParams ?? new Dictionary<string, object>();
            ResolvedTargets = resolvedTargets ?? new List<TargetInfo>();
            GeneratedEvents = generatedEvents ?? new List<IGameEvent>();
            GeneratedCards = generatedCards ?? new List<CardInstance>();
            GeneratedCardSnapshots = generatedCardSnapshots ?? new List<CardInstanceSnapshot>();
            Outputs = outputs ?? new Dictionary<string, object?>();
            ParentEffectSucceeded = parentEffectSucceeded;
            if (sourceCard != null)
            {
                Source = TargetInfo.ForCard(sourceCard);
            }

            OutputPayload = BuildOutputPayload(Outputs);
        }

        private static JsonObject BuildOutputPayload(IReadOnlyDictionary<string, object?> outputs)
        {
            var payload = new JsonObject();
            foreach (var kv in outputs)
            {
                if (kv.Value is JsonNode node)
                {
                    payload[kv.Key] = node;
                    continue;
                }

                payload[kv.Key] = JsonSerializer.SerializeToNode(kv.Value, kv.Value?.GetType() ?? typeof(object));
            }
            return payload;
        }

    }
    /// <summary>
    /// 原子事实事件：当玩家墓地数量发生变化时触发。
    /// </summary>
    public class GraveyardUpdatedEvent : GameEventBase
    {
        public int NewGraveyardCount { get; }

        public GraveyardUpdatedEvent(PlayerState player, int newGraveyardCount, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.GraveyardUpdated, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            NewGraveyardCount = newGraveyardCount;
        }
    }

    /// <summary>
    /// 原子事实事件：当卡牌或玩家的计数器发生变化时触发。
    /// </summary>
    [GameEventMetadata("计数器变化", "当卡牌或玩家的计数器发生变化时触发的事件。", EventType.CounterChanged)]
    public class CounterChangedEvent : GameEventBase
    {
        public string CounterName { get; }
        public int OldValue { get; }
        public int NewValue { get; }

        public CounterChangedEvent(PlayerState player, string counterName, int oldValue, int newValue, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.CounterChanged, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            CounterName = counterName ?? string.Empty;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public CounterChangedEvent(CardInstance card, string counterName, int oldValue, int newValue, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.CounterChanged, eventId, timestamp)
        {
            Source = TargetInfo.ForCard(card);
            CounterName = counterName ?? string.Empty;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    /// <summary>
    /// Event triggered when a unit's attack state (CanAttack) changes.
    /// </summary>
    public class AttackStateUpdatedEvent : GameEventBase
    {
        public bool CanAttack { get; }

        public AttackStateUpdatedEvent(CardInstance sourceCard, bool canAttack, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.AttackStateUpdated, eventId, timestamp)
        {
            Source = TargetInfo.ForCard(sourceCard);
            CanAttack = canAttack;
        }
    }

    /// <summary>
    /// Event triggered when a card's Act availability changes (typically after using Act).
    /// </summary>
    public class ActStateUpdatedEvent : GameEventBase
    {
        public int ActLastUsedTurnNumber { get; }

        public ActStateUpdatedEvent(CardInstance sourceCard, int actLastUsedTurnNumber, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.ActStateUpdated, eventId, timestamp)
        {
            Source = TargetInfo.ForCard(sourceCard);
            ActLastUsedTurnNumber = actLastUsedTurnNumber;
        }
    }

    /// <summary>
    /// Event triggered when a card is played using Enhance mode.
    /// </summary>
    [GameEventMetadata(
        "爆能",
        "当玩家以爆能方式打出卡牌时触发的事件。",
        EventType.Enhance)]
    public class EnhanceEvent : GameEventBase
    {
        [GameEventParameter("出牌玩家", "发动爆能的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? PlayPlayer => Source?.Player;

        [GameEventParameter("爆能卡牌", "以爆能方式打出的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? PlayedCard => Target.FirstOrDefault()?.Card;

        [GameEventParameter("支付费用", "本次爆能实际消耗的费用。")]
        public int PaidCost { get; }

        [GameEventParameter("触发能力ID", "本次爆能所对应的能力运行时ID。")]
        public Guid? TriggeredAbilityRuntimeId { get; }

        public EnhanceEvent(PlayerState player, CardInstance card, int paidCost, Guid? triggeredAbilityRuntimeId = null, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Enhance, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            Target.Add(TargetInfo.ForCard(card));
            PaidCost = paidCost;
            TriggeredAbilityRuntimeId = triggeredAbilityRuntimeId;
        }
    }

    /// <summary>
    /// Event triggered when a player's ability to evolve changes.
    /// </summary>
    public class EvolveStateUpdatedEvent : GameEventBase, ICommittableEvent
    {
        public bool CanEvolve { get; }

        public EvolveStateUpdatedEvent(PlayerState player, bool canEvolve, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.EvolveStateUpdated, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            CanEvolve = canEvolve;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var player = Source?.Player;
            if (player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "EvolveStateUpdatedEvent.Commit: player is null." };
            }

            player.CanEvolve = CanEvolve;
            return new DomainOperationResult { Success = true };
        }
    }

    /// <summary>
    /// Event triggered when a player's ability to super evolve changes.
    /// </summary>
    public class SuperEvolveStateUpdatedEvent : GameEventBase, ICommittableEvent
    {
        public bool CanSuperEvolve { get; }

        public SuperEvolveStateUpdatedEvent(PlayerState player, bool canSuperEvolve, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.SuperEvolveStateUpdated, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            CanSuperEvolve = canSuperEvolve;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var player = Source?.Player;
            if (player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "SuperEvolveStateUpdatedEvent.Commit: player is null." };
            }

            player.CanSuperEvolve = CanSuperEvolve;
            return new DomainOperationResult { Success = true };
        }
    }

    /// <summary>
    /// Event triggered when a player's PP+1 uses availability changes.
    /// </summary>
    [GameEventMetadata("PP+1次数更新", "当玩家的PP+1可用次数发生变化时触发。", EventType.PpBoostStateUpdated, visibility: EventMetadataVisibility.System)]
    public class PpBoostStateUpdatedEvent : GameEventBase
    {
        public int PpBoostUsesAvailable { get; }

        public PpBoostStateUpdatedEvent(PlayerState player, int usesAvailable, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.PpBoostStateUpdated, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            PpBoostUsesAvailable = usesAvailable;
        }
    }
}
