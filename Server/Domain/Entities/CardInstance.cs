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
using Microsoft.Extensions.DependencyInjection;
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
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For TargetInfo
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // For DomainOperationResult
using ShadowverseBeyond.Server.Services; // For GameDataProvider static instance
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;

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
        public const string SpellboostCounterKey = "spellboost";
        public const string EarthsigilCounterKey = "earthsigil";
        private static readonly string[] SystemCounterKeys = { SpellboostCounterKey, EarthsigilCounterKey };
        private static Dictionary<CardActionUsageType, int> CreateDefaultActionUsageCounts()
            => Enum.GetValues<CardActionUsageType>().ToDictionary(static actionType => actionType, static _ => 0);

        private static Dictionary<CardActionUsageType, int> CreateDefaultActionUsageLimits()
            => Enum.GetValues<CardActionUsageType>().ToDictionary(static actionType => actionType, static _ => 1);

        /// <summary>
        /// Unique identifier for this card instance within the current game.
        /// Now unified with DTO to use Guid type for consistency.
        /// </summary>
        [DslExposable("卡牌ID", Description = "卡牌的唯一标识符")]
        public Guid InstanceId { get; set; } = Guid.Empty;

        // Tracks whether this card preserved combat continuity between attack declaration and resolution.
        // Intentionally not DSL-exposed.
        internal long CombatContinuityVersion { get; private set; }

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
        [DslExposable("拥有者ID", Description = "拥有者的唯一标识符")]
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

        public int BaseCost => CurrentDefinition?.Cost ?? 0;

        /// <summary>
        /// The current play point cost of this card instance.
        /// This can differ from the base cost defined in CardDefinition due to card effects.
        /// Currently stored directly for backward compatibility.
        /// Future enhancement: Calculate as BaseCost + buff modifications.
        /// </summary>
        [DslExposable("当前费用", Description = "卡牌当前的费用（包含效果修正）")]
        public int CurrentCost
        {
            get
            {
                var context = GameServiceProvider.CurrentGameContext;
                return context == null
                    ? Math.Max(0, BaseCost)
                    : Math.Max(0, GameServiceProvider.BuffService.GetEffectiveCost(this, context));
            }
        }

        /// <summary>
        /// Base attack from CardDefinition (before any buffs or effects).
        /// </summary>
        public int BaseAttack => CurrentDefinition?.Attack ?? 0;

        /// <summary>
        /// Current attack value (for units/leaders).
        /// Currently stored directly for backward compatibility.
        /// Future enhancement: Calculate as BaseAttack + buff modifications.
        /// </summary>
        [DslExposable("当前攻击力", Description = "卡牌当前的攻击力（包含效果修正）")]
        public int CurrentAttack
        {
            get
            {
                var context = GameServiceProvider.CurrentGameContext;
                return context == null
                    ? Math.Max(0, BaseAttack)
                    : Math.Max(0, GameServiceProvider.BuffService.GetEffectiveAttack(this, context));
            }
        }

        /// <summary>
        /// Current health value (for units/leaders).
        /// Directly managed property that properly handles MaxHealth changes.
        /// </summary>
        public int Damage = 0;

        [DslExposable("当前生命值", Description = "卡牌当前的生命值")]
        public int CurrentHealth => MaxHealth - Damage;

        /// <summary>
        /// Base health from CardDefinition (before any buffs or effects).
        /// </summary>
        public int BaseHealth => CurrentDefinition?.Health ?? 0;

        /// <summary>
        /// Maximum health value (for units/leaders).
        /// </summary>
        [DslExposable("最大生命值", Description = "卡牌的最大生命值（包含效果修正）")]
        public int MaxHealth
        {
            get
            {
                var context = GameServiceProvider.CurrentGameContext;
                return context == null
                    ? Math.Max(0, BaseHealth)
                    : Math.Max(0, GameServiceProvider.BuffService.GetEffectiveMaxHealth(this, context));
            }
        }


        /// <summary>
        /// Whether this unit is evolved (for units only).
        /// </summary>
        [DslExposable("是否已进化", Description = "该单位是否已进化")]
        public bool IsEvolved { get; set; } = false;
        /// <summary>
        /// 是否处于超进化状态（进化的强化版，继承进化触发）。
        /// </summary>
        [DslExposable("是否已超进化", Description = "该单位是否已超进化")]
        public bool IsSuperEvolved { get; private set; } = false;

        /// <summary>
        /// Gets the effective keywords, combining base keywords from the definition and temporary ones.
        /// Note: This does not include keywords from buffs. The BuffService is responsible for the final list.
        /// </summary>
        [DslExposable("关键字", Description = "卡牌当前拥有的关键字列表")]
        public List<CardKeyword> Keywords
        {
            get
            {
                var context = GameServiceProvider.CurrentGameContext;
                return context == null
                    ? BaseKeywords
                    : GameServiceProvider.BuffService.GetEffectiveKeywords(this, context);
            }
        }

        /// <summary>
        /// Gets the effect keywords aggregated from this card's abilities (deduplicated).
        /// This does not filter by silence or ability enablement.
        /// </summary>
        [DslExposable("效果关键字", Description = "卡牌能力的效果关键字集合（去重，不包含None）")]
        public List<AbilityKeyword> AbilityKeywords => GetEffectiveAbilities()
            .SelectMany(a => a.AbilityKeywords ?? Array.Empty<AbilityKeywordConfig>())
            .Select(k => k.Keyword)
            .Where(k => k != AbilityKeyword.None)
            .Distinct()
            .ToList();

        [DslExposable("当前有效能力", Description = "卡牌当前生效中的完整能力列表")]
        public IReadOnlyList<Ability> EffectiveAbilities => GetEffectiveAbilities();

        /// <summary>
        /// Base keywords from the card's current definition.
        /// </summary>
        public List<CardKeyword> BaseKeywords => (CurrentDefinition?.Keywords ?? Enumerable.Empty<string>())
            .Select(k => Enum.TryParse<CardKeyword>(k, true, out var kw) ? kw : CardKeyword.None)
            .Where(k => k != CardKeyword.None)
            .ToList();

        /// <summary>
        /// Countdown value (for amulets, crests, and faith cards).
        /// </summary>
        [DslExposable("倒计时", Description = "护符、纹章与信仰的倒计时值")]
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

        [DslExposable("区域停留时间", Description = "卡牌在所在区域上停留了多少轮")]
        public int ZoneStayTurn { get; set; }

        /// <summary>
        /// Whether this card can attack this turn.
        /// </summary>
        [DslExposable("可以攻击", Description = "该卡牌本回合是否可以攻击")]
        public bool CanAttack =>
            !HasKeyword(CardKeyword.Shackle) &&
            (HasKeyword(CardKeyword.Rush) || HasKeyword(CardKeyword.Storm) || IsEvolved || ZoneStayTurn > 0) &&
            HasRemainingActionUsage(CardActionUsageType.Attack);

        /// <summary>
        /// Whether this card is silenced (loses all abilities and keywords but retains stat modifications).
        /// </summary>
        public bool IsSilenced { get; private set; } = false;

        // Evolvability is derived from card type and runtime state (no definition flag).

        /// <summary>
        /// Number of attacks made this turn.
        /// </summary>
        [DslExposable("当前攻击次数", Description = "该卡牌在回合中攻击的次数")]
        public int AttacksThisTurn
        {
            get => GetActionUsageCount(CardActionUsageType.Attack);
            set => SetActionUsageCount(CardActionUsageType.Attack, value);
        }

        /// <summary>
        /// Maximum number of attacks this card can perform per turn.
        /// </summary>
        [DslExposable("最大攻击次数", Description = "该卡牌每回合可攻击次数上限")]
        public int MaxAttacksPerTurn
        {
            get => GetActionUsageLimit(CardActionUsageType.Attack);
            set => SetBaseActionUsageLimit(CardActionUsageType.Attack, value);
        }

        /// <summary>
        /// 存储与此卡牌实例关联的能力的持久运行时状态（例如计数器）。
        /// Key: 变量名 (string), Value: 变量值 (object)
        /// </summary>
        public Dictionary<string, object> AbilityState { get; private set; } = [];

        private readonly List<string> _additionalTribes = new();
        private readonly List<string> _additionalNames = new();
        private readonly List<CardInstance> _fusedCards = new();
        private readonly Dictionary<CardActionUsageType, int> _actionUsageCounts = CreateDefaultActionUsageCounts();
        private readonly Dictionary<CardActionUsageType, int> _baseActionUsageLimits = CreateDefaultActionUsageLimits();

        private readonly Counters _counters;

        public Counters Counters => _counters;

        [DslExposable("变量", Description = "卡牌上的系统与临时计数器")]
        public IReadOnlyDictionary<string, int> CounterMap => _counters;

        [DslExposable("已融合卡牌列表", Description = "该卡牌已融合素材实例列表")]
        public IReadOnlyList<CardInstance> FusedCards => _fusedCards;

        public int GetActionUsageCount(CardActionUsageType actionType)
            => _actionUsageCounts.TryGetValue(actionType, out var value) ? Math.Max(0, value) : 0;

        public void SetActionUsageCount(CardActionUsageType actionType, int value)
        {
            _actionUsageCounts[actionType] = Math.Max(0, value);
        }

        public int GetBaseActionUsageLimit(CardActionUsageType actionType)
            => _baseActionUsageLimits.TryGetValue(actionType, out var value) ? Math.Max(0, value) : 1;

        public void SetBaseActionUsageLimit(CardActionUsageType actionType, int value)
        {
            _baseActionUsageLimits[actionType] = Math.Max(0, value);
        }

        public int GetActionUsageLimit(CardActionUsageType actionType)
        {
            var context = GameServiceProvider.CurrentGameContext;
            return context == null
                ? GetBaseActionUsageLimit(actionType)
                : Math.Max(0, GameServiceProvider.BuffService.GetEffectiveActionUsageLimit(this, actionType, context));
        }

        public bool HasRemainingActionUsage(CardActionUsageType actionType)
            => GetActionUsageCount(actionType) < GetActionUsageLimit(actionType);

        public void ConsumeActionUsage(CardActionUsageType actionType)
        {
            SetActionUsageCount(actionType, GetActionUsageCount(actionType) + 1);
        }

        public void ResetActionUsage(CardActionUsageType actionType)
        {
            SetActionUsageCount(actionType, 0);
        }

        // [DslExposable("重置前快照", Description = "卡牌上一次重置前保存的轻量快照")]
        // public CardInstanceResetSnapshot? LastPreResetSnapshot { get; private set; }

        [DslExposable("魔力增幅", Description = "spellboost 计数器数值")]
        public int Spellboost => GetCounter(SpellboostCounterKey);

        [DslExposable("土之印", Description = "earthsigil 计数器数值")]
        public int Earthsigil => GetCounter(EarthsigilCounterKey);

        /// <summary>
        /// Whether this card currently has at least one Enhance ability.
        /// </summary>
        public bool HasEnhanceAbility =>
            GetEffectiveAbilities().Any(a => a.AbilityKeywords.Any(k => k.Keyword == AbilityKeyword.Enhance));

        private IReadOnlyList<Ability> GetEffectiveAbilities()
        {
            var context = GameServiceProvider.CurrentGameContext;
            if (context?.AbilityManager != null)
            {
                return context.AbilityManager
                    .GetEffectiveAbilitiesForCard(InstanceId, context)
                    .OfType<Ability>()
                    .ToList();
            }

            return Abilities;
        }

        /// <summary>
        /// Computes the Act cost for this card, applying buff-based keyword modifiers.
        /// </summary>
        public int GetActCost(GameContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var actAbilities = context.AbilityManager?
                .GetEffectiveAbilitiesForCard(InstanceId, context)
                .Where(a => a.HasKeyword(AbilityKeyword.Act))
                .ToList()
                ?? new List<IAbility>();

            if (actAbilities.Count == 0)
            {
                return 0;
            }

            var buffService = context.BuffService;
            var costs = actAbilities
                .Select(a => GetActCostForAbility(a, context))
                .Where(value => value > 0)
                .DefaultIfEmpty(0)
                .Max();

            return costs;
        }

        /// <summary>
        /// Computes the Act cost for a specific ability on this card.
        /// </summary>
        public int GetActCostForAbility(IAbility ability, GameContext context)
        {
            if (ability == null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var baseValue = ability.GetKeywordValue(AbilityKeyword.Act)
                ?? (ability as Ability)?.Config?.ActivateCost
                ?? 0;

            return context.BuffService.GetEffectiveAbilityKeywordValue(this, AbilityKeyword.Act, baseValue, context);
        }

        /// <summary>
        /// Computes the Enhance cost for a specific ability on this card, applying buff-based keyword modifiers.
        /// </summary>
        public int GetEnhanceCostForAbility(IAbility ability, GameContext context)
        {
            if (ability == null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var baseValue = ability.GetKeywordValue(AbilityKeyword.Enhance) ?? 0;
            return context.BuffService.GetEffectiveAbilityKeywordValue(this, AbilityKeyword.Enhance, baseValue, context);
        }

        /// <summary>
        /// Computes all Enhance costs for this card from effective runtime abilities only.
        /// </summary>
        public IReadOnlyList<int> GetEnhanceCostCandidates(GameContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var costs = new List<int>();
            var enhanceAbilities = context.AbilityManager?
                .GetEffectiveAbilitiesForCard(InstanceId, context)
                .Where(a => a.HasKeyword(AbilityKeyword.Enhance))
                .ToList()
                ?? new List<IAbility>();

            if (enhanceAbilities.Count > 0)
            {
                foreach (var ability in enhanceAbilities)
                {
                    var cost = GetEnhanceCostForAbility(ability, context);
                    if (cost > 0)
                    {
                        costs.Add(cost);
                    }
                }
            }

            return costs.Distinct().ToList();
        }

        /// <summary>
        /// Number of Act activations used this turn.
        /// </summary>
        [DslExposable("当前启动次数", Description = "该卡牌在回合中启动的次数")]
        public int ActsThisTurn
        {
            get => GetActionUsageCount(CardActionUsageType.Act);
            set => SetActionUsageCount(CardActionUsageType.Act, value);
        }

        /// <summary>
        /// Maximum Act activations allowed per turn.
        /// </summary>
        [DslExposable("最大启动次数", Description = "该卡牌每回合可启动次数上限")]
        public int MaxActsPerTurn
        {
            get => GetActionUsageLimit(CardActionUsageType.Act);
            set => SetBaseActionUsageLimit(CardActionUsageType.Act, value);
        }

        /// <summary>
        /// Number of Fusion activations used this turn.
        /// </summary>
        [DslExposable("当前融合次数", Description = "该卡牌在回合中融合的次数")]
        public int FusionsThisTurn
        {
            get => GetActionUsageCount(CardActionUsageType.Fusion);
            set => SetActionUsageCount(CardActionUsageType.Fusion, value);
        }

        /// <summary>
        /// Maximum Fusion activations allowed per turn.
        /// </summary>
        [DslExposable("最大融合次数", Description = "该卡牌每回合可融合次数上限")]
        public int MaxFusionsPerTurn
        {
            get => GetActionUsageLimit(CardActionUsageType.Fusion);
            set => SetBaseActionUsageLimit(CardActionUsageType.Fusion, value);
        }

        public bool HasUsedActThisTurn()
            => !HasRemainingActionUsage(CardActionUsageType.Act);

        public void MarkActUsed()
        {
            ConsumeActionUsage(CardActionUsageType.Act);
        }

        public bool HasUsedFusionThisTurn()
            => !HasRemainingActionUsage(CardActionUsageType.Fusion);

        public void MarkFusionUsed()
        {
            ConsumeActionUsage(CardActionUsageType.Fusion);
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
        [DslExposable("原始卡牌", Description = "卡牌的原型，包含卡牌在无附加效果情况下的基础属性和定义")]
        public CardDefinition? CurrentDefinition => GameDataProvider.Instance?.GetCardDefinition(CurrentDefinitionId);

        /// <summary>
        /// Gets the card's name from its definition.
        /// </summary>
        public string Name => CurrentDefinition?.Name ?? $"Card_{CurrentDefinitionId}";

        /// <summary>
        /// Gets the list of card names this instance can be treated as.
        /// Includes the base definition name plus any runtime aliases.
        /// </summary>
        public List<string> CardNames
        {
            get
            {
                var baseName = Name;
                if (_additionalNames.Count == 0)
                {
                    return new List<string> { baseName };
                }

                var combined = new List<string> { baseName };
                foreach (var alias in _additionalNames)
                {
                    if (!combined.Any(existing => string.Equals(existing, alias, StringComparison.OrdinalIgnoreCase)))
                    {
                        combined.Add(alias);
                    }
                }

                return combined;
            }
        }

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
        [DslExposable("卡牌种类", Description = "卡牌所属的种类")]

        public List<string> Tribe
        {
            get
            {
                var baseTribes = CanonicalizeTribes(CurrentDefinition?.Tribe);
                if (_additionalTribes.Count == 0)
                {
                    return baseTribes;
                }

                var combined = new List<string>(baseTribes);
                foreach (var tribe in CanonicalizeTribes(_additionalTribes))
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
            _counters = new Counters(SystemCounterKeys, BuildInitialCounterDefaults(baseDef));
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
            _counters = new Counters(SystemCounterKeys);
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
        public IEnumerable<Ability> GetAbilitiesByKeyword(AbilityKeyword abilityTag)
        {
            return Abilities.Where(a => a.HasKeyword(abilityTag));
        }

        /// <summary>
        /// 检查是否拥有指定标签的能力
        /// </summary>
        /// <param name="abilityTag">能力标签</param>
        /// <returns>是否拥有该能力</returns>
        public bool HasAbility(AbilityKeyword abilityTag)
        {
            return GetEffectiveAbilities().Any(a => a.HasKeyword(abilityTag));
        }

        /// <summary>
        /// 验证是否可以攻击
        /// </summary>
        /// <returns>是否可以攻击</returns>
        public bool CanPerformAttack(GameState gameState, PlayerState attackerPlayer, CardInstance target, bool forceAttack = false)
        {
            if (forceAttack) return true;

            if (!IsFollower && !IsLeader)
            {
                return false;
            }

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

            if (!target.IsFollower && !target.IsLeader)
            {
                return false;
            }

            var opponentField = opponentPlayer.Field;
            static bool HasEffectiveWard(CardInstance follower)
            {
                return follower.HasKeyword(CardKeyword.Ward)
                       && !follower.HasKeyword(CardKeyword.Ambush)
                       && !follower.HasKeyword(CardKeyword.Pressure);
            }

            var wardFollowers = opponentField.Where(HasEffectiveWard).ToList();
            var ignoresWard = HasKeyword(CardKeyword.IgnoreWard);

            if (!ignoresWard && wardFollowers.Any() && !wardFollowers.Contains(target))
            {
                // If there are Ward followers, the target must be one of them.
                return false;
            }

            if (target.IsLeader && HasKeyword(CardKeyword.CannotAttackLeader))
            {
                return false;
            }

            if (target.IsFollower && HasKeyword(CardKeyword.CannotAttackFollower))
            {
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
                bool hasSummoningSickness = this.ZoneStayTurn == 0;
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
            return GetActionUsageLimit(CardActionUsageType.Attack);
        }

        private int GetMaxActsPerTurn()
        {
            return GetActionUsageLimit(CardActionUsageType.Act);
        }

        private int GetMaxFusionsPerTurn()
        {
            return GetActionUsageLimit(CardActionUsageType.Fusion);
        }


        public DomainOperationResult PerformAttackDeclared(CardInstance target)
        {
            var result = new DomainOperationResult();

            // Attack count consumption is committed in AttackEvent.Commit to keep request preflight reversible.
            result.GeneratedEvents.Add(new AttackEvent(this, target, consumeAttackCount: true));
            if (target.CardType != CardType.Leader)
            {
                result.GeneratedEvents.Add(new ClashEvent(this, target));
            }
            result.GeneratedEvents.Add(new CombatResolutionEvent(this, target));
            result.Success = true;
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
            var hasCounterAttack = target.CardType == CardType.Follower;
            var attackerHadBane = HasKeyword(CardKeyword.Bane);
            var defenderHadBane = hasCounterAttack && target.HasKeyword(CardKeyword.Bane);

            // Determine intended damage values; final mitigation is resolved in DamageEvent.Commit.
            var damageToTarget = CurrentAttack;
            result.GeneratedEvents.Add(new DamageEvent(target, damageToTarget, this, DamageKind.Combat));

            // Generate DamageEvent for the attacker (counter-attack)
            if (hasCounterAttack)
            {
                var damageToAttacker = target.CurrentAttack;
                result.GeneratedEvents.Add(new DamageEvent(this, damageToAttacker, target, DamageKind.Combat, isCounterAttack: true));
            }
            result.GeneratedEvents.Add(new CombatDestroyResolutionEvent(this, target, attackerHadBane, defenderHadBane, hasCounterAttack));

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
        /// Applies evolution to this card instance.
        /// </summary>
        /// <param name="evolutionOwner">进化卡牌所属的玩家。</param>
        /// <param name="evolutionSourceCard">能力进化时的来源卡牌（系统/手动进化时为 null）。</param>
        /// <param name="isManualEvolution">true 表示通过进化点手动进化；false 表示通过效果进化。</param>
        /// <returns>A result object containing generated events.</returns>
        public DomainOperationResult ApplyEvolution(PlayerState evolutionOwner, CardInstance? evolutionSourceCard, bool isManualEvolution = false)
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
            var evolutionSource = evolutionSourceCard != null
                ? TargetInfo.ForCard(evolutionSourceCard)
                : TargetInfo.ForPlayer(evolutionOwner);
            result.GeneratedEvents.Add(new EvolveEvent(this, evolutionSource, false, isManualEvolution));
            result.Success = true;

            return result;
        }

        /// <summary>
        /// Applies super evolution (enhanced evolution) to this card instance.
        /// Generates both evolve-related events and the stat change event for +3/+3.
        /// </summary>
        public DomainOperationResult ApplySuperEvolution(PlayerState evolutionOwner, CardInstance? evolutionSourceCard, bool isManualEvolution = false)
        {
            var result = new DomainOperationResult();

            if (!CanEvolve())
            {
                result.Success = false;
                result.ErrorMessage = "Card cannot super evolve (IsEvolved is true, not a follower, or BanEvolve present).";
                return result;
            }

            // 触发普通进化事件（便于沿用进化触发），同时触发超进化专用事件
            var evolutionSource = evolutionSourceCard != null
                ? TargetInfo.ForCard(evolutionSourceCard)
                : TargetInfo.ForPlayer(evolutionOwner);
            result.GeneratedEvents.Add(new EvolveEvent(this, evolutionSource, true, isManualEvolution));
            result.GeneratedEvents.Add(new SuperEvolveEvent(this, evolutionSource, isManualEvolution));
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
            result.GeneratedEvents.Add(new StatChangedEvent(this, attackModifier: 2, healthModifier: 2, costModifier: 0, durationType: DurationType.Permanent));
            result.Success = true;
            return result;
        }

        internal DomainOperationResult ApplySuperEvolutionChange(GameContext context)
        {
            IsEvolved = true;
            IsSuperEvolved = true;
            var result = new DomainOperationResult();
            result.GeneratedEvents.Add(new StatChangedEvent(this, attackModifier: 3, healthModifier: 3, costModifier: 0, durationType: DurationType.Permanent));
            result.Success = true;
            return result;
        }

        private void InitializeCountdown(CardDefinition? definition)
        {
            if (definition == null || (definition.Type != CardType.Amulet && definition.Type != CardType.Crest && definition.Type != CardType.Faith))
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
            ConsumeActionUsage(CardActionUsageType.Attack);
        }

        internal sealed record CardRuntimeStateSnapshot
        {
            public int Damage { get; init; }
            public bool IsEvolved { get; init; }
            public bool IsSuperEvolved { get; init; }
            public int? CurrentCountdown { get; init; }
            public bool IsSilenced { get; init; }
            public int ZoneStayTurn { get; init; }
            public Dictionary<CardActionUsageType, int> ActionUsageCounts { get; init; } = new();
            public Dictionary<CardActionUsageType, int> ActionUsageLimits { get; init; } = new();
            public Dictionary<string, object> AbilityState { get; init; } = new();
            public List<string> AdditionalNames { get; init; } = new();
            public List<string> AdditionalTribes { get; init; } = new();
            public List<CardInstance> FusedCards { get; init; } = new();
            public Dictionary<string, int> Counters { get; init; } = new();
        }

        internal CardRuntimeStateSnapshot CaptureRuntimeStateSnapshot()
        {
            return new CardRuntimeStateSnapshot
            {
                Damage = Damage,
                IsEvolved = IsEvolved,
                IsSuperEvolved = IsSuperEvolved,
                CurrentCountdown = CurrentCountdown,
                IsSilenced = IsSilenced,
                ZoneStayTurn = ZoneStayTurn,
                ActionUsageCounts = Enum.GetValues<CardActionUsageType>()
                    .ToDictionary(actionType => actionType, actionType => GetActionUsageCount(actionType)),
                ActionUsageLimits = Enum.GetValues<CardActionUsageType>()
                    .ToDictionary(actionType => actionType, actionType => GetBaseActionUsageLimit(actionType)),
                AbilityState = new Dictionary<string, object>(AbilityState),
                AdditionalNames = new List<string>(_additionalNames),
                AdditionalTribes = new List<string>(_additionalTribes),
                FusedCards = new List<CardInstance>(_fusedCards),
                Counters = new Dictionary<string, int>(_counters)
            };
        }

        internal void CopyRuntimeStateFrom(CardInstance source)
        {
            CopyRuntimeStateFrom(source.CaptureRuntimeStateSnapshot());
        }

        internal void CopyRuntimeStateFrom(CardRuntimeStateSnapshot source)
        {
            Damage = source.Damage;
            IsEvolved = source.IsEvolved;
            IsSuperEvolved = source.IsSuperEvolved;
            CurrentCountdown = source.CurrentCountdown;
            IsSilenced = source.IsSilenced;
            ZoneStayTurn = source.ZoneStayTurn;
            foreach (var actionType in Enum.GetValues<CardActionUsageType>())
            {
                SetActionUsageCount(actionType, source.ActionUsageCounts.TryGetValue(actionType, out var usageCount) ? usageCount : 0);
                SetBaseActionUsageLimit(actionType, source.ActionUsageLimits.TryGetValue(actionType, out var usageLimit) ? usageLimit : 1);
            }
            AbilityState = new Dictionary<string, object>(source.AbilityState);
            _additionalNames.Clear();
            _additionalNames.AddRange(source.AdditionalNames);
            _additionalTribes.Clear();
            _additionalTribes.AddRange(source.AdditionalTribes);
            _fusedCards.Clear();
            _fusedCards.AddRange(source.FusedCards);
            ResetCounters();
            foreach (var (key, value) in source.Counters)
            {
                SetCounter(key, value);
            }
        }

        internal void ResetAttackState()
        {
            ResetActionUsage(CardActionUsageType.Attack);
            ZoneStayTurn = 0;
        }

        internal void ResetActState()
        {
            ResetActionUsage(CardActionUsageType.Act);
        }

        internal void ResetFusionState()
        {
            ResetActionUsage(CardActionUsageType.Fusion);
        }

        internal void ResetToDefinitionState()
        {
            Damage = 0;
            IsEvolved = false;
            IsSuperEvolved = false;
            IsSilenced = false;
            ZoneStayTurn = 0;
            foreach (var actionType in Enum.GetValues<CardActionUsageType>())
            {
                ResetActionUsage(actionType);
                SetBaseActionUsageLimit(actionType, 1);
            }
            AbilityState.Clear();
            _additionalNames.Clear();
            _additionalTribes.Clear();
            _fusedCards.Clear();
            ResetCounters();

            var baseDef = GameDataProvider.Instance?.GetCardDefinition(BaseDefinitionId);
            InitializeCountdown(baseDef);
        }

        // internal void CapturePreResetSnapshot()
        // {
        //     LastPreResetSnapshot = CardInstanceResetSnapshot.From(this);
        // }
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

        private static IReadOnlyDictionary<string, int> BuildInitialCounterDefaults(CardDefinition? definition)
        {
            if (definition?.Keywords == null || definition.Keywords.Count == 0)
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            var defaults = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var hasEarthsigil = definition.Keywords.Any(keyword =>
                string.Equals(keyword, nameof(CardKeyword.EarthSigil), StringComparison.OrdinalIgnoreCase));
            if (hasEarthsigil)
            {
                defaults[EarthsigilCounterKey] = 1;
            }

            return defaults;
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

        /// <summary>
        /// 检查当前卡牌在给定破坏原因下是否可以被破坏。
        /// 仅能力破坏会受“无法破坏/土之印/己方回合超进化随从”规则影响。
        /// </summary>
        public bool CanBeDestroyed(GameContext context, EventReasonSource reason)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (reason != EventReasonSource.Ability)
            {
                return true;
            }

            var effectiveKeywords = context.BuffService.GetEffectiveKeywords(this, context);
            if (effectiveKeywords.Contains(CardKeyword.DestroyResist))
            {
                return false;
            }

            if (effectiveKeywords.Contains(CardKeyword.EarthSigil))
            {
                return false;
            }

            if (!IsSuperEvolved || !IsFollower)
            {
                return true;
            }

            var activePlayerId = context.CurrentGameState.CurrentPlayerId;
            return !activePlayerId.HasValue || OwnerPlayerId != activePlayerId.Value;
        }

        internal void SetLastMoveReason(CardMoveReason reason)
        {
            LastMoveReason = reason;
        }

        public void AddTribeMarker(string tribe)
        {
            var normalized = NormalizeTribeValue(tribe);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            if (_additionalTribes.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _additionalTribes.Add(normalized);
        }

        public void RemoveTribeMarker(string tribe)
        {
            var normalized = NormalizeTribeValue(tribe);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            var index = _additionalTribes.FindIndex(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _additionalTribes.RemoveAt(index);
            }
        }

        public void AddCardNameAlias(string cardName)
        {
            if (string.IsNullOrWhiteSpace(cardName))
            {
                return;
            }

            if (string.Equals(cardName, Name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (_additionalNames.Any(existing => string.Equals(existing, cardName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _additionalNames.Add(cardName);
        }

        private static List<string> CanonicalizeTribes(IEnumerable<string>? rawTribes)
        {
            var result = new List<string>();
            if (rawTribes == null)
            {
                return result;
            }

            foreach (var rawTribe in rawTribes)
            {
                var normalized = NormalizeTribeValue(rawTribe);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (result.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                result.Add(normalized);
            }

            return result;
        }

        private static string NormalizeTribeValue(string? rawTribe)
        {
            var trimmed = rawTribe?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return string.Empty;
            }

            var registry = GameServiceProvider.CurrentGameContext?.ServiceProvider.GetService<ITribeRegistry>();
            return registry?.Normalize(trimmed) ?? trimmed;
        }

        internal void AddFusedCards(IEnumerable<CardInstance> fusedCards)
        {
            if (fusedCards == null)
            {
                return;
            }

            foreach (var fusedCard in fusedCards)
            {
                if (fusedCard == null)
                {
                    continue;
                }

                AddFusedCard(fusedCard);
            }
        }

        internal void AddFusedCard(CardInstance fusedCard)
        {
            if (fusedCard == null || fusedCard.InstanceId == Guid.Empty || fusedCard.InstanceId == InstanceId)
            {
                return;
            }

            if (_fusedCards.Any(card => card.InstanceId == fusedCard.InstanceId))
            {
                return;
            }

            _fusedCards.Add(fusedCard);
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
            var normalizedAmount = Math.Max(0, amount);

            // Only generate the HealEvent; actual state change happens via HealthChangedEvent.Commit
            if (source != null)
            {
                result.GeneratedEvents.Add(new HealEvent(this, normalizedAmount, source));
            }
            else
            {
                result.GeneratedEvents.Add(new HealEvent(this, normalizedAmount, this));
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
            var abilityManager = context.AbilityManager;

            // Align transform cleanup with CardMovedEvent.ResetCardState:
            // remove buffs -> remove auras -> unregister abilities -> reset runtime state.
            var removeBuffsResult = context.BuffService.RemoveAllBuffs(this.InstanceId, context)
                ?? new DomainOperationResult { Success = true };
            if (!removeBuffsResult.Success)
            {
                return removeBuffsResult;
            }
            result.GeneratedEvents.AddRange(removeBuffsResult.GeneratedEvents);

            var auraResult = context.AuraManager.UnregisterBySourceCard(this.InstanceId, context);
            if (!auraResult.Success)
            {
                return auraResult;
            }
            result.GeneratedEvents.AddRange(auraResult.GeneratedEvents);

            abilityManager.UnregisterCardAbilities(this.InstanceId);

            // 2. Perform the transformation and reset runtime state to the new definition baseline.
            this.BaseDefinitionId = newDefinition.Id;
            ResetToDefinitionState();
            MarkCombatContinuityChanged();

            // 3. Get ability configurations for the new card definition.
            var newAbilityConfigs = GameDataProvider.Instance.GetAbilityConfigs(newDefinition.Id);
            if (newAbilityConfigs != null && newAbilityConfigs.Any())
            {
                // 4. Create and register new abilities for the transformed card.
                var abilities = abilityManager.CreateAbilitiesForCard(this, newAbilityConfigs, null, context.CurrentGameState);
                foreach (var ability in abilities)
                {
                    context.CurrentGameState.InGameIds.GetOrCreate(InGameIdKind.Ability, ability.RuntimeId);
                }
            }

            // 5. Generate the notification event (after transform)
            context.BuffService.InvalidateDerivedState(context.CurrentGameState, this.InstanceId);
            result.Success = true;

            return result;
        }

        internal void MarkCombatContinuityChanged()
        {
            CombatContinuityVersion++;
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
