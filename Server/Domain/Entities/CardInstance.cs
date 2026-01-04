// File: Server/Domain/Entities/CardInstance.cs
//
// 相关文档:
// - 数据库设计: 文档/04-数据库设计/01-实体关系图.md
// - 游戏事件系统: 文档/05-游戏系统/04-游戏事件系统.md
// - 战斗系统: 文档/05-游戏系统/02-战斗系统.md
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shadowverse.Domain.Interfaces; // For ICard
using ShadowverseBeyond.Server.Dtos; // For CardZone enum and CardInstanceDto
using ShadowverseBeyond.Server.Dtos.Enums; // For CardType enum
using ShadowverseBeyond.Server.Domain.Events; // For IGameEvent and GameContext
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters; // For DestroyEffectRequest
using ShadowverseBeyond.Server.Domain.Buffs; // For Buff system integration
using ShadowverseBeyond.Server.Domain.Abilities.Conditions; // For IConditionEvaluator
using ShadowverseBeyond.Server.Domain.Abilities; // For Ability class
using ShadowverseBeyond.Server.Domain.Abilities.Common; // For AbilityType
using ShadowverseBeyond.Server.Domain.Constants; // For CardKeyword enum
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For TargetId
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // For DomainOperationResult
using ShadowverseBeyond.Server.Services; // For GameDataProvider static instance
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;

namespace ShadowverseBeyond.Server.Domain.Entities
{
    /// <summary>
    /// Represents the state of a specific instance of a card within a game.
    /// This replaces CardState and all BoardEntityState subclasses for unified entity handling.
    /// Can represent cards in any zone (hand, field, deck, graveyard, etc.) and any type (leader, unit, amulet, spell).
    /// </summary>
    [DslExposable("卡牌实例", Description = "代表游戏中一张具体的卡牌实例")]
    public class CardInstance : ICard
    {
        private const string ActLastUsedTurnKey = "act.lastUsedTurnNumber";
        public const string SpellboostCounterKey = "spellboost";
        public const string EarthsigilCounterKey = "earthsigil";
        private static readonly string[] SystemCounterKeys = { SpellboostCounterKey, EarthsigilCounterKey };

        /// <summary>
        /// Unique identifier for this card instance within the current game.
        /// Now unified with DTO to use Guid type for consistency.
        /// </summary>
        public Guid InstanceId { get; set; } = Guid.Empty;

        /// <summary>
        /// Identifier linking to the static CardDefinition, providing base stats, text, type, etc.
        /// </summary>
        /// <summary>
        /// The DefinitionId of the card's original, non-evolved state. This should be immutable after creation.
        /// </summary>
        public int BaseDefinitionId { get; private set; }

        /// <summary>
        /// A computed property that returns the appropriate DefinitionId based on the current IsEvolved state.
        /// This is the core of the dynamic data switching.
        /// </summary>
        // 旧实现：进化后切换到进化定义
        // public int CurrentDefinitionId => IsEvolved ? EvolvedDefinitionId : BaseDefinitionId;
        // 超凡规则：进化/超进化不再切换卡牌定义，始终使用基础定义并通过状态加成调整身材
        public int CurrentDefinitionId => BaseDefinitionId;

        /// <summary>
        /// The player who owns this card instance.
        /// </summary>
        public Guid OwnerPlayerId { get; set; } // Changed from string to Guid

        /// <summary>
        /// The current zone where this card instance is located.
        /// </summary>
        [DslExposable("当前区域", Description = "卡牌当前所在的区域")]
        public CardZone CurrentZone { get; set; } = CardZone.Deck;

        [DslExposable("上次移动原因", Description = "卡牌最近一次移动的原因")]
        public CardMoveReason LastMoveReason { get; private set; } = CardMoveReason.Unknown;

        // --- Computed Properties from CardDefinition ---
        [DslExposable("卡牌类型", Description = "卡牌的类型（随从、法术、护符等）")]
        public CardType CardType => CurrentDefinition?.Type ?? CardType.Follower;

        [DslExposable("基础费用", Description = "卡牌的基础费用")]
        public int BaseCost => CurrentDefinition?.Cost ?? 0;

        /// <summary>
        /// The current play point cost of this card instance.
        /// This can differ from the base cost defined in CardDefinition due to card effects.
        /// Currently stored directly for backward compatibility.
        /// Future enhancement: Calculate as BaseCost + buff modifications.
        /// </summary>
        [DslExposable("当前费用", Description = "卡牌当前的费用（包含效果修正）")]
        public int CurrentCost => GameServiceProvider.BuffService.GetEffectiveCost(this, GameServiceProvider.CurrentGameContext
            ?? throw new InvalidOperationException("GameContext is not available via GameServiceProvider."));

        /// <summary>
        /// Base attack from CardDefinition (before any buffs or effects).
        /// </summary>
        [DslExposable("基础攻击力", Description = "卡牌的基础攻击力")]
        public int BaseAttack => CurrentDefinition?.Attack ?? 0;

        /// <summary>
        /// Current attack value (for units/leaders).
        /// Currently stored directly for backward compatibility.
        /// Future enhancement: Calculate as BaseAttack + buff modifications.
        /// </summary>
        [DslExposable("当前攻击力", Description = "卡牌当前的攻击力（包含效果修正）")]
        public int CurrentAttack => GameServiceProvider.BuffService.GetEffectiveAttack(this, GameServiceProvider.CurrentGameContext
            ?? throw new InvalidOperationException("GameContext is not available via GameServiceProvider."));

        /// <summary>
        /// Current health value (for units/leaders).
        /// Directly managed property that properly handles MaxHealth changes.
        /// </summary>
        public int Damage = 0;

        [DslExposable("当前生命值", Description = "卡牌当前的生命值")]
        public int CurrentHealth => Math.Max(0, MaxHealth - Damage);

        /// <summary>
        /// Base health from CardDefinition (before any buffs or effects).
        /// </summary>
        [DslExposable("基础生命值", Description = "卡牌的基础生命值")]
        public int BaseHealth => CurrentDefinition?.Health ?? 0;

        /// <summary>
        /// Maximum health value (for units/leaders).
        /// </summary>
        [DslExposable("最大生命值", Description = "卡牌的最大生命值（包含效果修正）")]
        public int MaxHealth => GameServiceProvider.BuffService.GetEffectiveMaxHealth(this, GameServiceProvider.CurrentGameContext
            ?? throw new InvalidOperationException("GameContext is not available via GameServiceProvider."));


        /// <summary>
        /// Whether this unit is evolved (for units only).
        /// </summary>
        [DslExposable("是否已进化", Description = "该单位是否已进化")]
        public bool IsEvolved { get; set; } = false;
        /// <summary>
        /// 是否处于超进化状态（进化的强化版，继承进化触发）。
        /// </summary>
        public bool IsSuperEvolved { get; private set; } = false;

        /// <summary>
        /// Gets the effective keywords, combining base keywords from the definition and temporary ones.
        /// Note: This does not include keywords from buffs. The BuffService is responsible for the final list.
        /// </summary>
        [DslExposable("关键字", Description = "卡牌当前拥有的关键字列表")]
        public List<CardKeyword> Keywords => GameServiceProvider.BuffService.GetEffectiveKeywords(this, GameServiceProvider.CurrentGameContext
            ?? throw new InvalidOperationException("GameContext is not available via GameServiceProvider."));

        /// <summary>
        /// Base keywords from the card's current definition.
        /// </summary>
        public List<CardKeyword> BaseKeywords => (CurrentDefinition?.Keywords ?? Enumerable.Empty<string>())
            .Select(k => Enum.TryParse<CardKeyword>(k, true, out var kw) ? kw : CardKeyword.None)
            .Where(k => k != CardKeyword.None)
            .ToList();

        /// <summary>
        /// Countdown value (for amulets and crests only).
        /// </summary>
        [DslExposable("倒计时", Description = "护符与纹章的倒计时值")]
        public int? Countdown => CurrentCountdown;

        /// <summary>
        /// Current countdown value for amulets. Null if not applicable.
        /// </summary>
        public int? CurrentCountdown { get; private set; }

        /// <summary>
        /// Index of the card within its current zone's list (e.g., hand, field, deck).
        /// Null if not applicable or not in an ordered zone.
        /// </summary>
        [DslExposable("卡牌位置", Description = "卡牌在所在区域的位置排序")]
        public int? ZoneIndex { get; set; }

        [DslExposable("战场生存时间", Description = "卡牌在战场上存活了多少轮")]
        public int FieldTurn { get; set; }

        /// <summary>
        /// Whether this card can attack this turn.
        /// </summary>
        [DslExposable("可以攻击", Description = "该卡牌本回合是否可以攻击")]
        public bool CanAttack =>
            !HasKeyword(CardKeyword.Shackle) &&
            (HasKeyword(CardKeyword.Rush) || HasKeyword(CardKeyword.Storm) || IsEvolved || FieldTurn > 0) &&
            AttacksThisTurn < GetMaxAttacksPerTurn();

        /// <summary>
        /// Whether this card is silenced (loses all abilities and keywords but retains stat modifications).
        /// </summary>
        public bool IsSilenced { get; private set; } = false;

        // Evolvability is derived from card type and runtime state (no definition flag).

        /// <summary>
        /// Number of attacks made this turn.
        /// </summary>
        public int AttacksThisTurn { get; set; } = 0;

        /// <summary>
        /// Maximum number of attacks this card can perform per turn.
        /// </summary>
        [DslExposable("最大攻击次数", Description = "该卡牌每回合可攻击次数上限")]
        public int MaxAttacksPerTurn { get; set; } = 1;

        /// <summary>
        /// 存储与此卡牌实例关联的能力的持久运行时状态（例如计数器）。
        /// Key: 变量名 (string), Value: 变量值 (object)
        /// </summary>
        public Dictionary<string, object> AbilityState { get; private set; } = [];

        private readonly List<string> _additionalTribes = new();

        private readonly CardCounters _counters = new(SystemCounterKeys);

        [DslExposable("计数器", Description = "卡牌上的系统与临时计数器")]
        public CardCounters Counters => _counters;

        [DslExposable("计数字典", Description = "计数器的只读字典视图")]
        public IReadOnlyDictionary<string, object?> CounterMap => _counters;

        [DslExposable("魔力增幅", Description = "spellboost 计数器数值")]
        public int Spellboost => GetCounter(SpellboostCounterKey);

        [DslExposable("土之印计数", Description = "earthsigil 计数器数值")]
        public int Earthsigil => GetCounter(EarthsigilCounterKey);

        /// <summary>
        /// Whether this card currently has at least one Activated (Act) ability.
        /// </summary>
        public bool HasActAbility => Abilities.Any(a => a.Type == AbilityType.Activated);

        /// <summary>
        /// The owner's turn number when this card last used Act. Null if never used.
        /// </summary>
        public int? ActLastUsedTurnNumber
        {
            get
            {
                if (!AbilityState.TryGetValue(ActLastUsedTurnKey, out var value) || value == null)
                    return null;

                return value switch
                {
                    int i => i,
                    long l => checked((int)l),
                    _ => null
                };
            }
        }

        /// <summary>
        /// Number of Act activations used this turn.
        /// </summary>
        public int ActsThisTurn { get; set; } = 0;

        /// <summary>
        /// Maximum Act activations allowed per turn.
        /// </summary>
        public int MaxActsPerTurn { get; set; } = 1;

        public bool HasUsedActThisTurn(int ownerTurnNumber)
            => ActsThisTurn >= GetMaxActsPerTurn();

        public void MarkActUsed(int ownerTurnNumber)
        {
            ActsThisTurn = Math.Max(0, ActsThisTurn + 1);
            AbilityState[ActLastUsedTurnKey] = ownerTurnNumber;
        }

        /// <summary>
        /// 此卡牌实例的所有能力列表（只读）。
        /// 能力的添加和移除必须通过AbilityManager进行，以确保数据一致性。
        /// </summary>
        public IReadOnlyList<Ability> Abilities { get; private set; } = Array.Empty<Ability>();

        /// <summary>
        /// Amount of damage taken by this card instance.
        /// Used for compatibility with legacy code that expects DamageTaken property.
        /// </summary>
        public int DamageTaken => Math.Max(0, MaxHealth - CurrentHealth);

        #region Static Data Accessors

        /// <summary>
        /// A private, cached accessor for the card's static definition.
        /// This avoids repeated lookups in the GameDataProvider within the same instance context.
        /// </summary>
        [DslExposable("卡牌原型", Description = "卡牌的原型，包含卡牌在无附加效果情况下的基础属性和定义")]
        public CardDefinition? CurrentDefinition => GameDataProvider.Instance?.GetCardDefinition(CurrentDefinitionId);

        /// <summary>
        /// Gets the card's name from its definition.
        /// </summary>
        public string Name => CurrentDefinition?.Name ?? $"Card_{CurrentDefinitionId}";

        /// <summary>
        /// Gets the card's description from its definition.
        /// </summary>
        public string Description => CurrentDefinition?.Description ?? string.Empty;

        /// <summary>
        /// Gets the card's rarity from its definition.
        /// </summary>
        public CardRarity Rarity => CurrentDefinition?.Rarity ?? CardRarity.Bronze;

        /// <summary>
        /// Gets the card's class from its definition.
        /// </summary>
        public CardClass Class => CurrentDefinition?.Class ?? CardClass.Neutral;

        /// <summary>
        /// Gets the card's tribe names from its definition.
        /// </summary>
        public List<string> Tribe
        {
            get
            {
                var baseTribes = CurrentDefinition?.Tribe ?? new List<string>();
                if (_additionalTribes.Count == 0)
                {
                    return baseTribes;
                }

                var combined = new List<string>(baseTribes);
                foreach (var tribe in _additionalTribes)
                {
                    if (!combined.Any(existing => string.Equals(existing, tribe, StringComparison.OrdinalIgnoreCase)))
                    {
                        combined.Add(tribe);
                    }
                }

                return combined;
            }
        }

        #endregion

        /// <summary>
        /// Constructor requiring essential information for a card instance.
        /// </summary>
        public CardInstance(Guid instanceId, int definitionId, Guid ownerPlayerId)
        {
            InstanceId = instanceId;
            BaseDefinitionId = definitionId;
            OwnerPlayerId = ownerPlayerId;

            var baseDef = GameDataProvider.Instance?.GetCardDefinition(definitionId);
            InitializeCountdown(baseDef);
            ResetCounters();
        }

        /// <summary>
        /// Parameterless constructor for ORM/Serialization.
        /// </summary>
        protected CardInstance()
        {
            InstanceId = Guid.Empty;
            OwnerPlayerId = Guid.Empty; // Use Guid.Empty for default
            ResetCounters();
        }

        /// <summary>
        /// 内部方法：设置卡牌的能力列表。
        /// 仅供AbilityManager调用，确保双向绑定的数据一致性。
        /// </summary>
        /// <param name="abilities">新的能力列表</param>
        internal void SetAbilities(IReadOnlyList<Ability> abilities)
        {
            Abilities = abilities ?? Array.Empty<Ability>();
        }

        /// <summary>
        /// 获取指定标签的能力
        /// </summary>
        /// <param name="abilityTag">能力标签</param>
        /// <returns>匹配的能力列表</returns>
        public IEnumerable<Ability> GetAbilitiesByKeyword(EffectKeyword abilityTag)
        {
            return Abilities.Where(a => a.HasKeyword(abilityTag));
        }

        /// <summary>
        /// 检查是否拥有指定标签的能力
        /// </summary>
        /// <param name="abilityTag">能力标签</param>
        /// <returns>是否拥有该能力</returns>
        public bool HasAbility(EffectKeyword abilityTag)
        {
            return Abilities.Any(a => a.HasKeyword(abilityTag));
        }

        /// <summary>
        /// 验证是否可以攻击
        /// </summary>
        /// <returns>是否可以攻击</returns>
        public bool CanPerformAttack(GameState gameState, PlayerState attackerPlayer, CardInstance target, bool forceAttack = false)
        {
            if (forceAttack) return true;

            // Basic state checks
            if (gameState.IsGameOver ||
                gameState.CurrentPlayerId != attackerPlayer.PlayerId ||
                !CanAttack ||
                CurrentZone != CardZone.Field ||
                AttacksThisTurn >= GetMaxAttacksPerTurn())
            {
                return false;
            }

            // Target validation logic
            var opponentPlayer = gameState.GetOpponentPlayerState(attackerPlayer.PlayerId);
            if (opponentPlayer == null || target.OwnerPlayerId != opponentPlayer.PlayerId) return false;

            var opponentField = opponentPlayer.Field;
            var wardFollowers = opponentField.Where(f => f.HasKeyword(CardKeyword.Ward)).ToList();

            if (wardFollowers.Any() && !wardFollowers.Contains(target))
            {
                // If there are Ward followers, the target must be one of them.
                return false;
            }

            if (target.IsFollower && target.HasKeyword(CardKeyword.Ambush))
            {
                // Ambush: cannot be declared as an attack target.
                return false;
            }

            if (target.IsFollower && target.HasKeyword(CardKeyword.Pressure))
            {
                // Pressure: cannot be declared as an attack target.
                return false;
            }

            if (target.IsLeader)
            {
                // To attack a leader, one of the following must be true:
                // 1. The attacker has Storm.
                // 2. The attacker is not under "summoning sickness" (i.e., it has been on the field for more than its entry turn).
                // Rush only allows attacking followers on the turn the card is played.
                bool hasSummoningSickness = this.FieldTurn == 0;
                if (!this.HasKeyword(CardKeyword.Storm) && hasSummoningSickness)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 获取本回合最大攻击次数
        /// </summary>
        /// <returns>最大攻击次数</returns>
        private int GetMaxAttacksPerTurn()
        {
            return Math.Max(0, MaxAttacksPerTurn);
        }

        private int GetMaxActsPerTurn()
        {
            return Math.Max(0, MaxActsPerTurn);
        }


        public DomainOperationResult PerformAttackDeclared(CardInstance target)
        {
            var result = new DomainOperationResult();

            // 记录攻击：直接通过内部 Apply 方法更新攻击计数与可攻击状态
            var oldCanAttack = this.CanAttack;
            this.ApplyAttackPerformed();
            if (oldCanAttack != this.CanAttack)
            {
                result.GeneratedEvents.Add(new AttackStateUpdatedEvent(this, this.CanAttack));
            }

            result.GeneratedEvents.Add(new AttackEvent(this, target));
            result.GeneratedEvents.Add(new ClashEvent(this, target));
            return result;
        }


        /// <summary>
        /// Generates the consequences of an attack without directly modifying state.
        /// This method calculates damage and creates the resulting sub-events (Clash, Damage).
        /// </summary>
        /// <param name="target">The target of the attack.</param>
        /// <returns>A DomainOperationResult containing the sequence of events that constitute the attack.</returns>
        public DomainOperationResult PerformAttack(CardInstance target)
        {
            var result = new DomainOperationResult();
            // Determine damage values
            result.GeneratedEvents.Add(new DamageEvent(target, CurrentAttack, this, DamageKind.Combat));

            // Generate DamageEvent for the attacker (counter-attack)
            if (target.CardType == CardType.Follower)
            {
                result.GeneratedEvents.Add(new DamageEvent(this, target.CurrentAttack, target, DamageKind.Combat));
            }

            result.Success = true;
            return result;
        }


        /// <summary>
        /// 验证是否可以进化（仅检查卡牌本体状态）。
        /// </summary>
        /// <returns>是否可以进化</returns>
        public bool CanEvolve()
        {
            // This method only checks intrinsic properties of the card itself.
            // All game-rule checks (EP cost, turn number, player's evolution right)
            // are handled by the committable event (e.g., EvolveRequestedEvent).
            return IsFollower && !IsEvolved && !BaseKeywords.Contains(CardKeyword.BanEvolve);
        }

        /// <summary>
        /// Applies evolution to this card instance using a new card definition.
        /// This method performs a "transformation" rather than just buffing stats.
        /// </summary>
        /// <param name="evolvedDefinition">The definition of the card to evolve into.</param>
        /// <returns>A result object containing generated events.</returns>
        public DomainOperationResult ApplyEvolution()
        {
            var result = new DomainOperationResult();

            if (!CanEvolve())
            {
                result.Success = false;
                result.ErrorMessage = "Card cannot evolve (IsEvolved is true, not a follower, or BanEvolve present).";
                return result;
            }

            // This method now only generates the event.
            // The actual state change is handled by the event's Commit method.
            result.GeneratedEvents.Add(new EvolveEvent(this, false));
            result.Success = true;

            return result;
        }

        /// <summary>
        /// Applies super evolution (enhanced evolution) to this card instance.
        /// Generates both evolve-related events and the stat change event for +3/+3.
        /// </summary>
        public DomainOperationResult ApplySuperEvolution()
        {
            var result = new DomainOperationResult();

            if (!CanEvolve())
            {
                result.Success = false;
                result.ErrorMessage = "Card cannot super evolve (IsEvolved is true, not a follower, or BanEvolve present).";
                return result;
            }

            // 触发普通进化事件（便于沿用进化触发），同时触发超进化专用事件
            result.GeneratedEvents.Add(new EvolveEvent(this, true));
            result.GeneratedEvents.Add(new SuperEvolveEvent(this));
            result.Success = true;

            return result;
        }

        /// <summary>
        /// 获取能够响应指定事件类型的能力
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <returns>能够响应该事件的能力列表</returns>
        public IEnumerable<Ability> GetAbilitiesTriggeredBy(EventType eventType)
        {
            return Abilities.Where(a => a.Trigger.EventTypes.Contains(eventType));
        }

        /// <summary>
        /// 获取当前激活的能力
        /// </summary>
        /// <returns>激活的能力列表</returns>
        public IEnumerable<Ability> GetActiveAbilities()
        {
            // 根据 Ability.cs 的注释，IsActive 已移除。
            // 激活状态由 AbilityManager 或通过检查 SourceCard.IsSilenced 等条件判断。
            // 此处暂时将“激活”理解为源卡牌未被沉默，或能力没有源卡牌。
            return Abilities.Where(a => (a.SourceCard != null));
        }

        #region Internal State Mutations
        /// <summary>
        /// 内部方法：应用进化的实体变更（定义切换、能力重建等）。当前实现超凡简化规则。
        /// </summary>
        internal DomainOperationResult ApplyEvolutionChange(GameContext context, bool isSuperEvolution = false)
        {
            IsEvolved = true;
            IsSuperEvolved = isSuperEvolution;

            if (isSuperEvolution)
            {
                return new DomainOperationResult { Success = true };
            }

            var result = new DomainOperationResult();
            result.GeneratedEvents.Add(new StatChangedEvent(this, attackModifier: 2, healthModifier: 2, costModifier: 0));
            result.Success = true;
            return result;
        }

        internal DomainOperationResult ApplySuperEvolutionChange(GameContext context)
        {
            IsEvolved = true;
            IsSuperEvolved = true;
            var result = new DomainOperationResult();
            result.GeneratedEvents.Add(new StatChangedEvent(this, attackModifier: 3, healthModifier: 3, costModifier: 0));
            result.Success = true;
            return result;
        }

        private void InitializeCountdown(CardDefinition? definition)
        {
            if (definition == null || (definition.Type != CardType.Amulet && definition.Type != CardType.Crest))
            {
                CurrentCountdown = null;
                return;
            }

            var hasCountdown = definition.Keywords?.Any(k =>
                string.Equals(k, nameof(CardKeyword.Countdown), StringComparison.OrdinalIgnoreCase)) ?? false;

            CurrentCountdown = hasCountdown && definition.Countdown.HasValue
                ? definition.Countdown
                : null;
        }

        internal void ApplyCountdownChange(int delta)
        {
            if (!CurrentCountdown.HasValue)
            {
                return;
            }

            var next = CurrentCountdown.Value + delta;
            CurrentCountdown = Math.Max(0, next);
        }

        /// <summary>
        /// 内部方法：应用生命值变更。正数表示治疗，负数表示伤害。
        /// 仅修改 Damage 字段，CurrentHealth = MaxHealth - Damage。
        /// </summary>
        internal void ApplyHealthChange(int changeAmount)
        {
            if (changeAmount == 0) return;
            if (changeAmount > 0)
            {
                Damage -= Math.Min(changeAmount, Damage);
            }
            else
            {
                Damage += (-changeAmount);
            }
        }
        /// <summary>
        /// 内部方法：记录一次攻击并按最大攻击次数更新 CanAttack。
        /// </summary>
        public void ApplyAttackPerformed()
        {
            AttacksThisTurn += 1;
        }

        internal void CopyRuntimeStateFrom(CardInstance source)
        {
            Damage = source.Damage;
            IsEvolved = source.IsEvolved;
            IsSuperEvolved = source.IsSuperEvolved;
            CurrentCountdown = source.CurrentCountdown;
            IsSilenced = source.IsSilenced;
            FieldTurn = source.FieldTurn;
            AttacksThisTurn = source.AttacksThisTurn;
            MaxAttacksPerTurn = source.MaxAttacksPerTurn;
            ActsThisTurn = source.ActsThisTurn;
            MaxActsPerTurn = source.MaxActsPerTurn;
            AbilityState = new Dictionary<string, object>(source.AbilityState);
            _additionalTribes.Clear();
            _additionalTribes.AddRange(source._additionalTribes);
            _counters.CopyFrom(source._counters);
        }

        internal void ResetAttackState()
        {
            AttacksThisTurn = 0;
            FieldTurn = 0;
        }

        internal void ResetActState()
        {
            ActsThisTurn = 0;
            AbilityState.Remove(ActLastUsedTurnKey);
        }

        internal void ResetToDefinitionState()
        {
            Damage = 0;
            IsEvolved = false;
            IsSuperEvolved = false;
            IsSilenced = false;
            FieldTurn = 0;
            AttacksThisTurn = 0;
            MaxAttacksPerTurn = 1;
            ActsThisTurn = 0;
            MaxActsPerTurn = 1;
            AbilityState.Clear();
            _additionalTribes.Clear();
            ResetCounters();

            var baseDef = GameDataProvider.Instance?.GetCardDefinition(BaseDefinitionId);
            InitializeCountdown(baseDef);
        }
        #endregion

        public int GetCounter(string key)
        {
            return _counters.Get(key);
        }

        public int SetCounter(string key, int value)
        {
            return _counters.Set(key, value);
        }

        public int AddCounter(string key, int delta)
        {
            return _counters.Add(key, delta);
        }

        public bool TryConsumeCounter(string key, int amount)
        {
            return _counters.TryConsume(key, amount);
        }

        private void ResetCounters()
        {
            _counters.Reset();
        }

        /// <summary>
        /// 获取能力数量
        /// </summary>
        /// <returns>能力总数</returns>
        public int GetAbilityCount()
        {
            return Abilities.Count;
        }

        // IBoardEntityState implementation
        Guid ICard.Uid => InstanceId;
        int ICard.Health
        {
            get => CurrentHealth;
            set => Damage = MaxHealth - value;
        }

        // Helper methods for type checking
        public bool IsLeader => CardType == CardType.Leader;
        public bool IsFollower => CardType == CardType.Follower;
        public bool IsSpell => CardType == CardType.Spell;
        public bool IsAmulet => CardType == CardType.Amulet;

        public bool IsDestroyed => CurrentHealth <= 0;

        // Helper methods for zone checking
        public bool IsInHand => CurrentZone == CardZone.Hand;
        public bool IsOnField => CurrentZone == CardZone.Field;
        public bool IsInDeck => CurrentZone == CardZone.Deck;
        public bool IsInGraveyard => CurrentZone == CardZone.Graveyard;

        // Keyword management methods are now removed.
        // All keyword logic is handled by the BuffService.
        // The HasKeyword method remains for convenience.
        /// <summary>
        /// 检查是否拥有指定关键字
        /// </summary>
        /// <param name="keyword">关键字枚举</param>
        /// <returns>如果拥有该关键字则返回true</returns>
        public bool HasKeyword(CardKeyword keyword)
        {
            return Keywords.Contains(keyword);
        }

        /// <summary>
        /// 检查是否拥有指定关键字（字符串版本，用于向后兼容）
        /// </summary>
        /// <param name="keyword">关键字名称</param>
        /// <returns>如果拥有该关键字则返回true</returns>
        public bool HasKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return false;

            // 尝试将字符串转换为CardKeyword枚举
            if (Enum.TryParse<CardKeyword>(keyword, true, out var keywordEnum))
            {
                return Keywords.Contains(keywordEnum);
            }

            return false;
        }

        internal void SetLastMoveReason(CardMoveReason reason)
        {
            LastMoveReason = reason;
        }

        public void AddTribeMarker(string tribe)
        {
            if (string.IsNullOrWhiteSpace(tribe))
            {
                return;
            }

            if (_additionalTribes.Any(existing => string.Equals(existing, tribe, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _additionalTribes.Add(tribe);
        }




        // Leader-specific methods (only applicable when IsLeader is true)

        /// <summary>
        /// Internal method to apply actual health changes and generate HealthChangedEvent.
        /// </summary>

        /// <summary>
        /// Applies damage to this card instance and returns generated events.
        /// </summary>
        /// <param name="amount">Amount of damage to apply (should be positive).</param>
        /// <param name="source">Source of the damage (optional).</param>
        /// <returns>DomainOperationResult containing DamageEvent and potentially HealthChangedEvent and DestroyEffectRequest.</returns>
        public DomainOperationResult TakeDamage(int amount, CardInstance? source = null)
        {
            var result = new DomainOperationResult { Success = true };
            if (amount <= 0) return result;

            // Only generate the DamageEvent; actual state change happens via HealthChangedEvent.Commit
            if (source != null)
            {
                result.GeneratedEvents.Add(new DamageEvent(this, amount, source, DamageKind.Ability));
            }
            else
            {
                // Allow self-damage event if source is null by falling back to self
                result.GeneratedEvents.Add(new DamageEvent(this, amount, this, DamageKind.Ability));
            }
            return result;
        }

        /// <summary>
        /// Applies healing to this card instance and returns generated events.
        /// </summary>
        /// <param name="amount">Amount of healing to apply (should be positive).</param>
        /// <param name="source">Source of the healing (optional).</param>
        /// <returns>DomainOperationResult containing HealEvent and potentially HealthChangedEvent.</returns>
        public DomainOperationResult Heal(int amount, CardInstance? source = null)
        {
            var result = new DomainOperationResult { Success = true };
            if (amount <= 0) return result;

            // Only generate the HealEvent; actual state change happens via HealthChangedEvent.Commit
            if (source != null)
            {
                result.GeneratedEvents.Add(new HealEvent(this, amount, source));
            }
            else
            {
                result.GeneratedEvents.Add(new HealEvent(this, amount, this));
            }
            return result;
        }


        /// <summary>
        /// 【领域方法】将此卡牌实例变形为另一个卡牌定义。
        /// </summary>
        /// <param name="newDefinition">新的卡牌定义。</param>
        /// <returns>包含操作结果的DomainOperationResult。</returns>
        public DomainOperationResult Transform(CardDefinition newDefinition, GameContext context)
        {
            var result = new DomainOperationResult();
            var abilityManager = context.ServiceProvider.GetRequiredService<IAbilityManager>();

            // 1. Unregister all old abilities associated with this card instance.
            abilityManager.UnregisterCardAbilities(this.InstanceId);

            // 2. Perform the basic transformation on the card instance
            this.BaseDefinitionId = newDefinition.Id;
            this.Damage = 0; // Reset damage on transform
            InitializeCountdown(newDefinition);

            // 3. Get ability configurations for the new card definition.
            var newAbilityConfigs = GameDataProvider.Instance.GetAbilityConfigs(newDefinition.Id);
            if (newAbilityConfigs != null && newAbilityConfigs.Any())
            {
                // 4. Create and register new abilities for the transformed card.
                abilityManager.CreateAbilitiesForCard(this, newAbilityConfigs);
            }

            // 5. Generate the notification event (after transform)
            context.BuffService.InvalidateDerivedState(context.CurrentGameState, this.InstanceId);
            result.GeneratedEvents.Add(new TransformEvent(this));
            result.Success = true;

            return result;
        }



        public DomainOperationResult ApplySilence(CardInstance? sourceCard = null, Ability? sourceAbility = null)
        {
            var result = new DomainOperationResult();
            if (IsSilenced)
            {
                result.Success = true; // Already silenced, no change.
                return result;
            }

            IsSilenced = true;
            // Note: Deactivating abilities and buffs is handled by AbilityManager and BuffManager listening to the event.
            result.GeneratedEvents.Add(new SilenceAppliedEvent(this, sourceCard, sourceAbility));
            result.Success = true;
            return result;
        }

        public DomainOperationResult RemoveSilence()
        {
            var result = new DomainOperationResult();
            if (!IsSilenced)
            {
                result.Success = true; // Not silenced, no change.
                return result;
            }

            IsSilenced = false;
            // Note: Re-activating abilities and buffs is handled by AbilityManager and BuffManager listening to the event.
            result.GeneratedEvents.Add(new SilenceRemovedEvent(this, this));
            result.Success = true;
            return result;
        }

        /// <summary>
        /// 【修复】从CardDefinition初始化CardInstance的所有属性
        // InitializeFromDefinition method is now removed as it's redundant.
        // All base properties are computed directly from the CardDefinition.

        // Amulet-specific methods (only applicable when IsAmulet is true)
        /// <summary>
        /// Decrements countdown and returns generated events.
        /// </summary>
        /// <returns>List of events generated by countdown tick</returns>
        // All buff-aware and stat calculation methods have been removed.
        // The CardInstance is now a pure state container.
        // All logic for buffs, stats, etc., will be handled by dedicated services
        // which operate on this state.
    } // Closing brace for CardInstance class
} // Closing brace for namespace
