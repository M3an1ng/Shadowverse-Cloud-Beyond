using AutoMapper; // Added for IMapper
//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 战斗系统: 文档/05-游戏系统/02-战斗系统.md
// - 回合管理系统: 文档/05-游戏系统/03-回合管理系统.md
//
using ShadowverseBeyond.Server.Domain.Entities;
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
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // Add using for GetService

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a card is played from hand.
    /// This is the initial event that may trigger Fanfare abilities.
    /// </summary>
    public class PlayCardEvent : GameEventBase
    {
        public CardInstance Card { get; }

        public PlayCardEvent(PlayerState player, CardInstance card)
            : base(EventType.PlayCard)
        {
            Source = TargetInfo.ForPlayer(player);
            Card = card;
        }
    }

    /// <summary>
    /// Event triggered when a card enters the battlefield (summoned).
    /// This is the natural trigger point for Fanfare abilities.
    /// </summary>
    public class SummonEvent : GameEventBase
    {
        public CardInstance Card { get; }
        public int? Position { get; }

        public SummonEvent(PlayerState player, CardInstance card, int? position = null)
            : base(EventType.Summon)
        {
            Source = TargetInfo.ForPlayer(player);
            Card = card;
            Position = position;
        }
    }

    /// <summary>
    /// Event triggered when damage is dealt to a target.
    /// </summary>
    public class DamageEvent : GameEventBase
    {
        public CardInstance Target { get; }
        public int Damage { get; }

        public DamageEvent(CardInstance target, int damage, CardInstance source)
            : base(EventType.Damage)
        {
            Target = target;
            Damage = damage;
            Source = TargetInfo.ForCard(source);

        }
    }

    /// <summary>
    /// Event triggered when a card is destroyed/dies.
    /// This is the natural trigger point for Last Words abilities.
    /// </summary>
    public class CardDestroyedEvent : GameEventBase
    {

        public CardInstance Target;

        public string? Reason;

        public CardDestroyedEvent(CardInstance source, CardInstance target, string reason = "Effect")
            : base(EventType.Destroy)
        {
            Source = TargetInfo.ForCard(source);
            Target = target;
            Reason = reason;
        }
    }


    /// <summary>
    /// 进化事件
    /// Event triggered when a card evolves.
    /// This is the natural trigger point for Evolve Effect abilities.
    /// </summary>
    public class EvolveEvent : GameEventBase
    {
        public EvolveEvent(CardInstance evolvedCard)
            : base(EventType.Evolve)
        {
            Source = TargetInfo.ForCard(evolvedCard);
        }
    }

    /// <summary>
    /// 攻击事件
    /// Event triggered when an attack is declared.
    /// </summary>
    public class AttackEvent : GameEventBase
    {
        public TargetInfo Target { get; }

        public AttackEvent(CardInstance attacker, TargetInfo target)
            : base(EventType.Attack)
        {
            Source = TargetInfo.ForCard(attacker);
            Target = target;
        }
    }

    /// <summary>
    /// Event triggered when two units engage in combat (clash).
    /// This event is fired before damage calculation and allows abilities to trigger.
    /// </summary>
    public class ClashEvent : GameEventBase
    {
        public CardInstance Defender { get; }

        public ClashEvent(CardInstance attacker, CardInstance defender)
            : base(EventType.Clash)
        {
            Source = TargetInfo.ForCard(attacker);
            Defender = defender;
        }
    }
    /// <summary>
    /// Event triggered when a card instance's health changes.
    /// </summary>
    public class HealthChangedEvent : GameEventBase
    {
        public CardInstance Target { get; }
        public int OldHealth { get; }
        public int NewHealth { get; }
        public int ChangeAmount { get; } // Positive for heal, negative for damage (absolute value of damage)

        public HealthChangedEvent(CardInstance target, int oldHealth, int newHealth, int maxHealth, int changeAmount, CardInstance sourceCard)
            : base(EventType.HealthChanged)
        {
            Target = target;
            OldHealth = oldHealth;
            NewHealth = newHealth;
            ChangeAmount = changeAmount;

            Source = TargetInfo.ForCard(sourceCard);
        }
    }

    /// <summary>
    /// Event triggered when healing is applied to a target.
    /// </summary>
    public class HealEvent : GameEventBase
    {
        public CardInstance Target { get; }
        public int HealAmount { get; }

        public HealEvent(CardInstance target, int healAmount, CardInstance source)
            : base(EventType.Heal)
        {
            Target = target;
            HealAmount = healAmount;
            Source = TargetInfo.ForCard(source);
        }
    }

    /// <summary>
    /// 换牌完成事件
    /// Event triggered when a player completes the mulligan phase.
    /// </summary>
    public class MulliganCompletedEvent : GameEventBase
    {
        public List<Guid> ReplacedCardUids { get; }

        public MulliganCompletedEvent(PlayerState player, List<Guid> replacedCardUids)
            : base(EventType.MulliganCompleted)
        {
            Source = TargetInfo.ForPlayer(player);
            ReplacedCardUids = replacedCardUids;
        }
    }

    /// <summary>
    /// Event triggered when a player's hand is updated (e.g., after mulligan, drawing cards, etc.).
    /// This event notifies the frontend about changes to the hand state.
    /// </summary>
    public class HandUpdatedEvent : GameEventBase
    {
        public List<CardInstance> UpdatedHand { get; }

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
    public class CountdownFinishedEvent : GameEventBase
    {
        public CardInstance Amulet { get; }

        public CountdownFinishedEvent(PlayerState player, CardInstance amulet)
            : base(EventType.CountdownFinished)
        {
            Source = TargetInfo.ForPlayer(player);
            Amulet = amulet;
        }
    }

    /// <summary>
    /// Event triggered when a player draws a card.
    /// </summary>
    /// <summary>
    /// Event triggered when a player draws one or more cards.
    /// This event contains the specific instances of the cards that were drawn.
    /// </summary>
    public class DrawEvent : GameEventBase
    {
        public List<Guid> DrawnCardInstanceIds { get; }

        public DrawEvent(PlayerState player, List<Guid> drawnCardInstanceIds)
            : base(EventType.Draw)
        {
            Source = TargetInfo.ForPlayer(player);
            DrawnCardInstanceIds = drawnCardInstanceIds ?? new List<Guid>();
        }
    }

    /// <summary>
    /// 回合开始通知事件
    /// 【架构修正】Events只做通知，不包含效果逻辑
    /// Event triggered to notify that a player's turn has started.
    /// </summary>
    public class TurnStartEvent : GameEventBase
    {
        public TurnStartEvent(PlayerState startingPlayer)
            : base(EventType.TurnStart)
        {
            Source = TargetInfo.ForPlayer(startingPlayer);
        }
    }

    /// <summary>
    /// 阶段转换完成事件
    /// 【架构修正】用于通知阶段转换完成，触发后续效果请求入队
    /// Event triggered when a game phase transition is completed.
    /// </summary>
    public class PhaseTransitionCompletedEvent : GameEventBase
    {
        public GamePhase FromPhase { get; }
        public GamePhase ToPhase { get; }

        public PhaseTransitionCompletedEvent(PlayerState transitionPlayer, GamePhase fromPhase, GamePhase toPhase)
            : base(EventType.PhaseTransition)
        {
            Source = TargetInfo.ForPlayer(transitionPlayer);
            FromPhase = fromPhase;
            ToPhase = toPhase;
        }
    }

    /// <summary>
    /// 回合结束通知事件
    /// 【架构修正】Events只做通知，不包含效果逻辑
    /// Event triggered to notify that a player's turn has ended.
    /// </summary>
    public class TurnEndEvent : GameEventBase
    {
        public TurnEndEvent(PlayerState endingPlayer)
            : base(EventType.TurnEnd)
        {
            Source = TargetInfo.ForPlayer(endingPlayer);
        }
    }

    /// <summary>
    /// 回合开始通知事件
    /// Event triggered to notify that a player's turn has started.
    /// 【架构修正】Events只做通知，不包含效果逻辑
    /// </summary>
    public class TurnStartedEvent : GameEventBase
    {
        public TurnStartedEvent(PlayerState startingPlayer)
            : base(EventType.TurnStart)
        {
            Source = TargetInfo.ForPlayer(startingPlayer);
        }
    }

    /// <summary>
    /// Event triggered when a Buff is successfully applied (registered) to a target.
    /// </summary>
    public class BuffAppliedEvent : GameEventBase
    {
        /// <summary>
        /// The Buff instance that was applied.
        /// Contains details about the source, target, duration, and effects.
        /// </summary>
        public BuffConfig AppliedBuff { get; }

        public BuffAppliedEvent(BuffConfig appliedBuff, CardInstance source)
            : base(EventType.BuffApplied)
        {
            AppliedBuff = appliedBuff ?? throw new ArgumentNullException(nameof(appliedBuff));
            Source = TargetInfo.ForCard(source);
        }

    }

    /// <summary>
    /// Event triggered when a Buff is removed from a target.
    /// </summary>
    public class BuffRemovedEvent : GameEventBase
    {
        /// <summary>
        /// The Buff instance that was removed.
        /// </summary>
        public BuffConfig RemovedBuff { get; }

        /// <summary>
        /// The reason for buff removal (e.g., "expired", "silenced", "replaced", "manual").
        /// </summary>
        public string RemovalReason { get; }

        public BuffRemovedEvent(BuffConfig removedBuff, CardInstance source, string removalReason = "manual")
            : base(EventType.BuffRemoved)
        {
            RemovedBuff = removedBuff ?? throw new ArgumentNullException(nameof(removedBuff));
            RemovalReason = removalReason ?? "unknown";
            Source = TargetInfo.ForCard(source);
        }

    }

    /// <summary>
    /// Event triggered when silence is applied to a target.
    /// </summary>
    public class SilenceAppliedEvent : GameEventBase
    {
        /// <summary>
        /// The target card instance that was silenced.
        /// </summary>
        public CardInstance Target { get; }

        /// <summary>
        /// The source ability that applied the silence (if any).
        /// </summary>
        public Ability? SourceAbility { get; }

        public SilenceAppliedEvent(CardInstance target, CardInstance? sourceCard = null, Ability? sourceAbility = null)
            : base(EventType.SilenceApplied)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
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
    public class SilenceRemovedEvent : GameEventBase
    {
        /// <summary>
        /// The target card instance that had
        /// The target card instance that had silence removed.
        /// </summary>
        public CardInstance Target { get; }

        public SilenceRemovedEvent(CardInstance target, CardInstance source)
            : base(EventType.SilenceRemoved)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
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
    /// Event triggered when a specific card is discarded from a player's hand.
    /// </summary>
    public class CardDiscardedEvent : GameEventBase
    {
        public Guid CardInstanceId { get; }
        public string Reason { get; }

        public CardDiscardedEvent(PlayerState player, Guid cardInstanceId, string reason)
            : base(EventType.Discard)
        {
            Source = TargetInfo.ForPlayer(player);
            CardInstanceId = cardInstanceId;
            Reason = reason;
        }
    }

    /// <summary>
    /// Event triggered when a card instance's stats (Attack, Health, MaxHealth) change.
    /// </summary>
    public class StatChangedEvent : GameEventBase
    {
        public Guid TargetCardInstanceId { get; }
        public int NewAttack { get; }
        public int NewMaxHealth { get; } // Changed from NewHealth, represents MaxHealth
        public int NewCost { get; }

        public StatChangedEvent(PlayerState sourcePlayer, Guid targetCardInstanceId, int newAttack, int newMaxHealth, int newCost)
            : base(EventType.StatChanged)
        {
            Source = TargetInfo.ForPlayer(sourcePlayer);
            TargetCardInstanceId = targetCardInstanceId;
            NewAttack = newAttack;
            NewMaxHealth = newMaxHealth;
            NewCost = newCost;
        }
    }
    /// <summary>
    /// Event triggered when a player's Evolution Points (EP) change.
    /// </summary>
    public class EpUpdateEvent : GameEventBase
    {
        public int CurrentEp { get; }
        public int MaxEp { get; }

        public EpUpdateEvent(PlayerState player, int currentEp, int maxEp)
            : base(EventType.EpUpdated)
        {
            Source = TargetInfo.ForPlayer(player);
            CurrentEp = currentEp;
            MaxEp = maxEp;
        }
    }
    /// <summary>
    /// Represents an event where a keyword is added to a card instance.
    /// </summary>
    public class KeywordAddedEvent : GameEventBase // Inherit from GameEventBase
    {
        /// <summary>
        /// The keyword that was added.
        /// </summary>
        public CardKeyword KeywordAdded { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeywordAddedEvent"/> class.
        /// </summary>
        /// <param name="keywordAdded">The keyword that was added.</param>
        public KeywordAddedEvent(CardKeyword keywordAdded, CardInstance source)
            : base(EventType.KeywordApplied)
        {
            KeywordAdded = keywordAdded;
            Source = TargetInfo.ForCard(source);
        }

        /// <summary>
        /// This event typically doesn't execute further logic itself.
        /// </summary>

        // Removed ToDto method, conversion should happen elsewhere or via AutoMapper profiles
    }
    /// <summary>
    /// Event triggered when a player's Play Points (PP) change.
    /// </summary>
    public class PpUpdateEvent : GameEventBase
    {
        public int CurrentPp { get; }
        public int MaxPp { get; }

        public PpUpdateEvent(PlayerState player, int currentPp, int maxPp)
            : base(EventType.PpUpdated)
        {
            Source = TargetInfo.ForPlayer(player);
            CurrentPp = currentPp;
            MaxPp = maxPp;
        }
    }

    /// <summary>
    /// Event triggered when a keyword is removed from a card.
    /// </summary>
    public class KeywordRemovedEvent : GameEventBase
    {
        public CardKeyword Keyword { get; }

        public KeywordRemovedEvent(CardKeyword keyword, CardInstance source)
            : base(EventType.KeywordRemoved)
        {
            Keyword = keyword;
            Source = TargetInfo.ForCard(source);
        }

    }
    /// <summary>
    /// Event triggered when a card is transformed into another.
    /// </summary>
    public class TransformEvent : GameEventBase
    {
        public int NewCardDefinitionId { get; }

        public TransformEvent(CardInstance transformedCard)
            : base(EventType.Transform)
        {
            if (transformedCard == null) throw new ArgumentNullException(nameof(transformedCard));
            Source = TargetInfo.ForCard(transformedCard);
            NewCardDefinitionId = transformedCard.DefinitionId;
        }
    }

    /// <summary>
    /// Event triggered when a card is vanished from the game.
    /// </summary>
    public class CardVanishedEvent : GameEventBase
    {
        public CardZone OriginalZone { get; }
        public string VanishReason { get; }

        public CardVanishedEvent(CardInstance card, CardZone originalZone, string vanishReason = "Effect")
            : base(EventType.Vanish)
        {
            Source = TargetInfo.ForCard(card);
            OriginalZone = originalZone;
            VanishReason = vanishReason;
        }
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
    /// 原子事实事件：当一张卡牌在不同区域之间移动时触发。
    /// </summary>
    public class CardMovedEvent : GameEventBase
    {
        public CardZone FromZone { get; }
        public CardZone ToZone { get; }

        public CardMovedEvent(CardInstance card, CardZone fromZone, CardZone toZone)
            : base(EventType.CardMoved)
        {
            FromZone = fromZone;
            ToZone = toZone;
            Source = TargetInfo.ForCard(card);
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
    public class EffectCompletedEvent : GameEventBase
    {
        /// <summary>
        /// 原始效果的类型字符串 (来自 EffectConfig.Type)。
        /// </summary>
        public string OriginalEffectType { get; }

        /// <summary>
        /// 原始效果的输入参数 (经过DSL初步解析和类型转换后的值)。
        /// </summary>
        public IReadOnlyDictionary<string, object> OriginalEffectInputParams { get; }

        /// <summary>
        /// 效果执行前解析出的最终目标列表。
        /// </summary>
        public IReadOnlyList<TargetInfo> ResolvedTargets { get; }

        /// <summary>
        /// 根据 EffectConfig.Outputs 配置和DSL表达式计算得出的输出参数。
        /// </summary>
        public IReadOnlyDictionary<string, object?> Outputs { get; }

        /// <summary>
        /// 发起此效果的玩家ID。
        /// </summary>
        public Guid EffectSourcePlayerId { get; }

        // SourceCard (来自 GameEventBase): 产生此效果的源卡牌实例。

        /// <summary>
        /// 可选：如果需要引用具体的 Ability 运行时实例ID。
        /// </summary>
        public Guid? SourceAbilityInstanceId { get; }


        public EffectCompletedEvent(
            string originalEffectType,
            IReadOnlyDictionary<string, object> originalEffectInputParams,
            IReadOnlyList<TargetInfo> resolvedTargets,
            IReadOnlyDictionary<string, object?> outputs,
                        Guid effectSourcePlayerId,
            CardInstance? sourceCard,
            Guid? sourceAbilityInstanceId = null
        ) : base(EventType.EffectCompleted)
        {
            OriginalEffectType = originalEffectType ?? string.Empty;
            OriginalEffectInputParams = originalEffectInputParams ?? new Dictionary<string, object>();
            ResolvedTargets = resolvedTargets ?? new List<TargetInfo>();
            Outputs = outputs ?? new Dictionary<string, object?>();
            EffectSourcePlayerId = effectSourcePlayerId;
            SourceAbilityInstanceId = sourceAbilityInstanceId;

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
} // This closing brace should match the namespace ShadowverseBeyond.Server.Domain.Events
