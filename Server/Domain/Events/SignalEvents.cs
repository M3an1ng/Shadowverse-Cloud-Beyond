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
    [GameEventMetadata("战斗冲突", "当两个单位进行战斗时触发的事件。", EventType.Clash)]
    public class ClashEvent : GameEventBase
    {
        [GameEventParameter("攻击者", "发起攻击的卡牌。")]
        public CardInstance? Attacker => Source?.Card;

        [GameEventParameter("防御者", "被攻击的卡牌。")]
        public CardInstance? Defender => Target.FirstOrDefault()?.Card;

        public ClashEvent(CardInstance attacker, CardInstance defender)
            : base(EventType.Clash)
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

        [GameEventParameter("目标玩家", "手牌被更新的玩家。")]
        public PlayerState? TargetPlayer => Source?.Player;

        public HandUpdatedEvent(PlayerState player, List<CardInstance> updatedHand)
            : base(EventType.HandUpdated)
        {
            Source = TargetInfo.ForPlayer(player);
            UpdatedHand = updatedHand;
        }
    }

    /// <summary>
    /// Event triggered when an amulet's countdown reaches zero.
    /// </summary>
    [GameEventMetadata("倒计时结束", "当护符的倒计时归零时触发的事件。", EventType.CountdownFinished)]
    public class CountdownFinishedEvent : GameEventBase
    {
        [GameEventParameter("拥有者", "护符的拥有者玩家。")]
        public PlayerState? Owner => Source?.Player;

        [GameEventParameter("护符", "倒计时结束的护符。")]
        public CardInstance? Amulet => Target.FirstOrDefault()?.Card;

        public CountdownFinishedEvent(PlayerState player, CardInstance amulet)
            : base(EventType.CountdownFinished)
        {
            Source = TargetInfo.ForPlayer(player);
            Target.Add(TargetInfo.ForCard(amulet));
        }
    }





    /// <summary>
    /// Event triggered when silence is applied to a target.
    /// </summary>
    [GameEventMetadata("沉默应用", "当一张卡牌被沉默时触发的事件。", EventType.SilenceApplied)]
    public class SilenceAppliedEvent : GameEventBase
    {
        /// <summary>
        /// The source ability that applied the silence (if any).
        /// </summary>
        [GameEventParameter("来源能力", "应用沉默的能力。")]
        public Ability? SourceAbility { get; }

        [GameEventParameter("沉默来源", "应用沉默的卡牌。")]
        public CardInstance? SilenceSource => Source?.Card;

        [GameEventParameter("沉默目标", "被沉默的卡牌。")]
        public CardInstance? SilenceTarget => Target.FirstOrDefault()?.Card;

        public SilenceAppliedEvent(CardInstance target, CardInstance? sourceCard = null, Ability? sourceAbility = null)
            : base(EventType.SilenceApplied)
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
    [GameEventMetadata("沉默移除", "当一张卡牌的沉默效果被移除时触发的事件。", EventType.SilenceRemoved)]
    public class SilenceRemovedEvent : GameEventBase
    {
        [GameEventParameter("移除来源", "移除沉默的卡牌。")]
        public CardInstance? RemovalSource => Source?.Card;

        [GameEventParameter("移除目标", "沉默被移除的卡牌。")]
        public CardInstance? RemovalTarget => Target.FirstOrDefault()?.Card;

        public SilenceRemovedEvent(CardInstance target, CardInstance source)
            : base(EventType.SilenceRemoved)
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
    /// Event triggered when a card is returned to a player's hand from the field.
    /// </summary>
    public class CardReturnedToHandEvent : GameEventBase
    {
        public CardReturnedToHandEvent(CardInstance returnedCard)
            : base(EventType.Return)
        {
            if (returnedCard == null) throw new ArgumentNullException(nameof(returnedCard));
            Source = TargetInfo.ForCard(returnedCard);
        }
    }
    /// <summary>
    /// 原子事实事件：当玩家牌库数量发生变化时触发。
    /// </summary>
    public class DeckCountChangedEvent : GameEventBase
    {
        public int NewDeckCount { get; }

        public DeckCountChangedEvent(PlayerState player, int newDeckCount)
            : base(EventType.DeckUpdated)
        {
            Source = TargetInfo.ForPlayer(player);
            NewDeckCount = newDeckCount;
        }
    }

    /// <summary>
    /// 原子事实事件：当卡牌因手牌已满而被爆牌时触发。
    /// </summary>
    public class CardBurnedEvent : GameEventBase
    {
        public string Reason { get; } // e.g., "HandFull"

        public CardBurnedEvent(CardInstance card, string reason = "HandFull")
            : base(EventType.CardBurned)
        {
            Source = TargetInfo.ForCard(card);
            Reason = reason;
        }
    }

    /// <summary>
    /// 原子事实事件：当玩家试图抽牌但牌库为空时触发。
    /// </summary>
    public class DeckEmptyOnDrawEvent : GameEventBase
    {
        public DeckEmptyOnDrawEvent(PlayerState player)
            : base(EventType.DeckEmptyOnDraw)
        {
            Source = TargetInfo.ForPlayer(player);
        }
    }

    /// <summary>
    /// 效果处理器聚合事实事件：当任何效果处理器完成其主要操作后触发。
    /// </summary>
    [DslExposable("效果完成事件", Description = "效果执行完成后产生的聚合事实事件，包含输入参数、目标与输出等信息")]
    public class EffectCompletedEvent : GameEventBase, IRawEventBackedGameEvent
    {
        /// <summary>
        /// 原始效果的类型字符串 (来自 EffectConfig.Type)。
        /// </summary>
        [DslExposable("效果类型", Description = "该 EffectCompletedEvent 对应的效果类型")]
        public EffectType OriginalEffectType { get; }

        /// <summary>
        /// 原始效果的输入参数 (经过DSL初步解析和类型转换后的值)。
        /// </summary>
        [DslExposable("输入参数", Description = "效果处理器的输入参数（解析后）")]
        public IReadOnlyDictionary<string, object> OriginalEffectInputParams { get; }

        /// <summary>
        /// 效果执行前解析出的最终目标列表。
        /// </summary>
        [DslExposable("解析目标", Description = "效果执行时使用的目标列表")]
        public IReadOnlyList<TargetInfo> ResolvedTargets { get; }

        /// <summary>
        /// 根据 EffectConfig.Outputs 配置和DSL表达式计算得出的输出参数。
        /// </summary>
        [DslExposable("输出参数", Description = "根据 Outputs DSL 计算出的输出字典")]
        public IReadOnlyDictionary<string, object?> Outputs { get; }

        public JsonObject OutputPayload { get; }

        /// <summary>
        /// 发起此效果的玩家ID。
        /// </summary>
        [DslExposable("发起玩家ID", Description = "发起该效果的玩家ID")]
        public Guid EffectSourcePlayerId { get; }

        // SourceCard (来自 GameEventBase): 产生此效果的源卡牌实例。

        /// <summary>
        /// 可选：如果需要引用具体的 Ability 运行时实例ID。
        /// </summary>
        [DslExposable("来源能力ID", Description = "可选：来源 Ability 的运行时实例ID")]
        public Guid? SourceAbilityInstanceId { get; }


        public EffectCompletedEvent(
            EffectType originalEffectType,
            IReadOnlyDictionary<string, object> originalEffectInputParams,
            IReadOnlyList<TargetInfo> resolvedTargets,
            IReadOnlyDictionary<string, object?> outputs,
                        Guid effectSourcePlayerId,
            CardInstance? sourceCard,
            Guid? sourceAbilityInstanceId = null
        ) : base(EventType.EffectCompleted)
        {
            OriginalEffectType = originalEffectType;
            OriginalEffectInputParams = originalEffectInputParams ?? new Dictionary<string, object>();
            ResolvedTargets = resolvedTargets ?? new List<TargetInfo>();
            Outputs = outputs ?? new Dictionary<string, object?>();
            EffectSourcePlayerId = effectSourcePlayerId;
            SourceAbilityInstanceId = sourceAbilityInstanceId;
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

        public GraveyardUpdatedEvent(PlayerState player, int newGraveyardCount)
            : base(EventType.GraveyardUpdated)
        {
            Source = TargetInfo.ForPlayer(player);
            NewGraveyardCount = newGraveyardCount;
        }
    }

    /// <summary>
    /// Event triggered when a unit's attack state (CanAttack) changes.
    /// </summary>
    public class AttackStateUpdatedEvent : GameEventBase
    {
        public bool CanAttack { get; }

        public AttackStateUpdatedEvent(CardInstance sourceCard, bool canAttack)
            : base(EventType.AttackStateUpdated)
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

        public ActStateUpdatedEvent(CardInstance sourceCard, int actLastUsedTurnNumber)
            : base(EventType.ActStateUpdated)
        {
            Source = TargetInfo.ForCard(sourceCard);
            ActLastUsedTurnNumber = actLastUsedTurnNumber;
        }
    }

    /// <summary>
    /// Event triggered when Fusion has been resolved and materials have been consumed.
    /// This is a UI-focused signal event (animation/log) and does not mutate state.
    /// </summary>
    public class CardsFusedEvent : GameEventBase, ILogAnnounceableEvent
    {
        public Guid FusingPlayerId { get; }
        public IReadOnlyList<Guid> FusedCardInstanceIds { get; }

        public int FusedCount => FusedCardInstanceIds.Count;

        public CardsFusedEvent(PlayerState player, IReadOnlyList<Guid> fusedCardInstanceIds)
            : base(EventType.CardsFused)
        {
            Source = TargetInfo.ForPlayer(player);
            FusingPlayerId = player.PlayerId;
            FusedCardInstanceIds = fusedCardInstanceIds ?? Array.Empty<Guid>();
        }

        public string GetAnnouncement(GameState gameState)
            => $"fusion player={gameState.InGameIds.Format(InGameIdKind.Player, FusingPlayerId)} count={FusedCount}";
    }

    /// <summary>
    /// Event triggered when a player's ability to evolve changes.
    /// </summary>
    public class EvolveStateUpdatedEvent : GameEventBase
    {
        public bool CanEvolve { get; }

        public EvolveStateUpdatedEvent(PlayerState player, bool canEvolve)
            : base(EventType.EvolveStateUpdated)
        {
            Source = TargetInfo.ForPlayer(player);
            CanEvolve = canEvolve;
        }
    }

    /// <summary>
    /// Event triggered when a player's ability to super evolve changes.
    /// </summary>
    public class SuperEvolveStateUpdatedEvent : GameEventBase
    {
        public bool CanSuperEvolve { get; }

        public SuperEvolveStateUpdatedEvent(PlayerState player, bool canSuperEvolve)
            : base(EventType.SuperEvolveStateUpdated)
        {
            Source = TargetInfo.ForPlayer(player);
            CanSuperEvolve = canSuperEvolve;
        }
    }
}
