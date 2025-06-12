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
using ShadowverseBeyond.Server.Domain.Events; // For IGameEvent and GameContext
using ShadowverseBeyond.Server.Domain.Buffs; // For Buff system integration
using ShadowverseBeyond.Server.Domain.Abilities.Conditions; // For IConditionEvaluator
using ShadowverseBeyond.Server.Domain.Abilities; // For Ability class
using ShadowverseBeyond.Server.Domain.Constants; // For CardKeyword enum

namespace ShadowverseBeyond.Server.Domain.Entities
{
    /// <summary>
    /// Represents the state of a specific instance of a card within a game.
    /// This replaces CardState and all BoardEntityState subclasses for unified entity handling.
    /// Can represent cards in any zone (hand, field, deck, graveyard, etc.) and any type (leader, unit, amulet, spell).
    /// </summary>
    public class CardInstance : ICard
    {
        /// <summary>
        /// Unique identifier for this card instance within the current game.
        /// Now unified with DTO to use Guid type for consistency.
        /// </summary>
        public Guid InstanceId { get; set; } = Guid.Empty;

        /// <summary>
        /// Identifier linking to the static CardDefinition, providing base stats, text, type, etc.
        /// </summary>
        public int DefinitionId { get; set; }

        /// <summary>
        /// The player who owns this card instance.
        /// </summary>
        public Guid OwnerPlayerId { get; set; } // Changed from string to Guid

        /// <summary>
        /// The current zone where this card instance is located.
        /// </summary>
        public CardZone CurrentZone { get; set; } = CardZone.Deck;

        /// <summary>
        /// The type of this card (Follower, Spell, Amulet, Leader).
        /// Gets the card type from the associated CardDefinition.
        /// </summary>
        public string CardType { get; set; } = string.Empty; // Keep as settable for now to maintain compatibility

        /// <summary>
        /// Base cost from CardDefinition (before any buffs or effects).
        /// </summary>
        public int BaseCost { get; set; }

        /// <summary>
        /// The current play point cost of this card instance.
        /// This can differ from the base cost defined in CardDefinition due to card effects.
        /// Currently stored directly for backward compatibility.
        /// Future enhancement: Calculate as BaseCost + buff modifications.
        /// </summary>
        public int CurrentCost { get; set; }

        /// <summary>
        /// Base attack from CardDefinition (before any buffs or effects).
        /// </summary>
        public int BaseAttack { get; set; }

        /// <summary>
        /// Current attack value (for units/leaders).
        /// Currently stored directly for backward compatibility.
        /// Future enhancement: Calculate as BaseAttack + buff modifications.
        /// </summary>
        public int CurrentAttack { get; set; }

        /// <summary>
        /// Current health value (for units/leaders).
        /// Directly managed property that properly handles MaxHealth changes.
        /// </summary>
        private int _currentHealth = 0;

        public int CurrentHealth
        {
            get => _currentHealth;
            set => _currentHealth = Math.Max(0, Math.Min(value, MaxHealth)); // Ensure within bounds
        }

        /// <summary>
        /// Base health from CardDefinition (before any buffs or effects).
        /// </summary>
        public int BaseHealth { get; set; }

        /// <summary>
        /// Maximum health value (for units/leaders).
        /// </summary>
        private int _maxHealth = 0;

        public int MaxHealth
        {
            get => _maxHealth;
            set
            {
                var oldMaxHealth = _maxHealth;
                _maxHealth = value;

                // Adjust CurrentHealth when MaxHealth changes
                if (oldMaxHealth != value)
                {
                    AdjustCurrentHealthForMaxHealthChange(oldMaxHealth, value);
                }
            }
        }

        /// <summary>
        /// Adjusts CurrentHealth when MaxHealth changes according to Shadowverse rules.
        /// </summary>
        /// <param name="oldMaxHealth">Previous maximum health value</param>
        /// <param name="newMaxHealth">New maximum health value</param>
        private void AdjustCurrentHealthForMaxHealthChange(int oldMaxHealth, int newMaxHealth)
        {
            if (oldMaxHealth == 0)
            {
                // Initial setting, set CurrentHealth to MaxHealth
                _currentHealth = newMaxHealth;
            }
            else if (newMaxHealth > oldMaxHealth)
            {
                // MaxHealth increased, increase CurrentHealth by the same amount
                var increase = newMaxHealth - oldMaxHealth;
                _currentHealth += increase;
            }
            else if (newMaxHealth < oldMaxHealth)
            {
                // MaxHealth decreased, cap CurrentHealth to new MaxHealth
                _currentHealth = Math.Min(_currentHealth, newMaxHealth);
            }

            // Ensure CurrentHealth is never negative
            _currentHealth = Math.Max(0, _currentHealth);
        }

        /// <summary>
        /// Whether this unit is evolved (for units only).
        /// </summary>
        public bool IsEvolved { get; set; } = false;

        /// <summary>
        /// 当前卡牌实例拥有的关键字列表（包括基础关键字和通过Buff获得的关键字）
        /// </summary>
        public List<CardKeyword> Keywords { get; set; } = [];

        /// <summary>
        /// 基础关键字（来自卡牌定义，不会被移除）
        /// </summary>
        public List<CardKeyword> BaseKeywords { get; set; } = [];

        /// <summary>
        /// 临时关键字（通过Buff获得，可以被移除）
        /// </summary>
        public List<CardKeyword> TemporaryKeywords { get; set; } = [];

        /// <summary>
        /// Countdown value (for amulets only).
        /// </summary>
        public int? Countdown { get; set; }

        /// <summary>
        /// Position on the field (0-4 for field entities, null otherwise).
        /// For now, keeping as int? for backward compatibility.
        /// Future enhancement: Create a dedicated Position class with zone and index properties.
        /// </summary>
        public int? FieldPosition { get; set; }

        /// <summary>
        /// Whether this card can attack this turn.
        /// </summary>
        public bool CanAttack { get; set; } = false;

        /// <summary>
        /// Whether this card is silenced (loses all abilities and keywords but retains stat modifications).
        /// </summary>
        public bool IsSilenced { get; set; } = false;

        // Removed CanEvolve property - evolution eligibility is determined by game rules and player state

        /// <summary>
        /// Number of attacks made this turn.
        /// </summary>
        public int AttacksThisTurn { get; set; } = 0;

        /// <summary>
        /// Active effects on this card instance. (Type object for now, consider specific Effect class later)
        /// </summary>
        public List<object> ActiveEffects { get; set; } = [];

        /// <summary>
        /// 存储与此卡牌实例关联的能力的持久运行时状态（例如计数器）。
        /// Key: 变量名 (string), Value: 变量值 (object)
        /// </summary>
        public Dictionary<string, object> AbilityState { get; private set; } = [];

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

        /// <summary>
        /// Name of the card (from card definition).
        /// This is a computed property that should be populated from card definition.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of the card (from card definition).
        /// This is a computed property that should be populated from card definition.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Rarity of the card (from card definition).
        /// This is a computed property that should be populated from card definition.
        /// </summary>
        public string Rarity { get; set; } = string.Empty;

        /// <summary>
        /// Class/Craft of the card (from card definition).
        /// This is a computed property that should be populated from card definition.
        /// </summary>
        public string Class { get; set; } = string.Empty;

        /// <summary>
        /// Tribe/Type of the card (from card definition).
        /// This is a computed property that should be populated from card definition.
        /// </summary>
        public List<string>? Tribe { get; set; }

        /// <summary>
        /// Constructor requiring essential information for a card instance.
        /// </summary>
        public CardInstance(Guid instanceId, int definitionId, Guid ownerPlayerId) // Changed parameter type
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            OwnerPlayerId = ownerPlayerId;

            // Initialize keyword lists
            RefreshKeywords();
        }

        /// <summary>
        /// Constructor that generates a new Guid for InstanceId.
        /// </summary>
        public CardInstance(int definitionId, Guid ownerPlayerId) // Changed parameter type
        {
            InstanceId = Guid.NewGuid();
            DefinitionId = definitionId;
            OwnerPlayerId = ownerPlayerId;

            // Initialize keyword lists
            RefreshKeywords();
        }

        /// <summary>
        /// Parameterless constructor for ORM/Serialization.
        /// </summary>
        protected CardInstance()
        {
            InstanceId = Guid.Empty;
            OwnerPlayerId = Guid.Empty; // Use Guid.Empty for default
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
        public IEnumerable<Ability> GetAbilitiesByTag(string abilityTag)
        {
            if (string.IsNullOrEmpty(abilityTag))
                return Enumerable.Empty<Ability>();

            return Abilities.Where(a => a.AbilityTag.Equals(abilityTag, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 检查是否拥有指定标签的能力
        /// </summary>
        /// <param name="abilityTag">能力标签</param>
        /// <returns>是否拥有该能力</returns>
        public bool HasAbility(string abilityTag)
        {
            if (string.IsNullOrEmpty(abilityTag))
                return false;

            return Abilities.Any(a => a.AbilityTag.Equals(abilityTag, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 验证是否可以攻击
        /// </summary>
        /// <returns>是否可以攻击</returns>
        public bool CanPerformAttack()
        {
            // 基础攻击条件检查
            if (!CanAttack)
                return false;

            if (CurrentAttack <= 0)
                return false;

            // 检查是否已达到本回合最大攻击次数
            var maxAttacksPerTurn = GetMaxAttacksPerTurn();
            if (AttacksThisTurn >= maxAttacksPerTurn)
                return false;

            return true;
        }

        /// <summary>
        /// 获取本回合最大攻击次数
        /// </summary>
        /// <returns>最大攻击次数</returns>
        private int GetMaxAttacksPerTurn()
        {
            // 默认每回合可攻击1次
            // TODO: 检查是否有增加攻击次数的能力或Buff
            return 1;
        }

        /// <summary>
        /// 执行攻击操作
        /// </summary>
        /// <param name="target">攻击目标</param>
        /// <returns>生成的事件列表</returns>
        public List<IGameEvent> PerformAttack(CardInstance target)
        {
            var events = new List<IGameEvent>();

            // 记录攻击次数
            AttacksThisTurn++;

            // 计算伤害
            int damage = CurrentAttack;

            // 对目标造成伤害
            var damageEvents = target.TakeDamage(damage, this);
            events.AddRange(damageEvents);

            // 如果目标有反击能力且存活，则反击
            if (target.CurrentHealth > 0 && target.CurrentAttack > 0 && target.CardType != "Spell")
            {
                var counterAttackEvents = this.TakeDamage(target.CurrentAttack, target);
                events.AddRange(counterAttackEvents);
            }

            // TODO: 生成攻击事件
            // var attackEvent = new UnitAttackEvent(this.InstanceId, target.InstanceId, damage);
            // events.Add(attackEvent);

            return events;
        }

        /// <summary>
        /// 攻击玩家（领袖）
        /// </summary>
        /// <param name="targetPlayer">目标玩家</param>
        /// <returns>生成的事件列表</returns>
        public List<IGameEvent> AttackPlayer(PlayerState targetPlayer)
        {
            var events = new List<IGameEvent>();

            // 记录攻击次数
            AttacksThisTurn++;

            // 计算伤害
            int damage = CurrentAttack;

            // 对玩家领袖造成伤害
            var damageEvents = targetPlayer.Leader.TakeDamage(damage, this);
            events.AddRange(damageEvents);

            // TODO: 生成攻击玩家事件
            // var attackPlayerEvent = new AttackPlayerEvent(this.InstanceId, targetPlayer.PlayerId, damage);
            // events.Add(attackPlayerEvent);

            return events;
        }

        /// <summary>
        /// 验证是否可以进化
        /// </summary>
        /// <param name="ownerPlayer">拥有者玩家</param>
        /// <param name="currentTurn">当前回合数</param>
        /// <param name="evolutionPointCost">进化所需EP</param>
        /// <param name="firstPlayerEvolutionStartTurn">先手玩家进化开始回合</param>
        /// <param name="secondPlayerEvolutionStartTurn">后手玩家进化开始回合</param>
        /// <returns>是否可以进化</returns>
        public bool CanEvolve(PlayerState ownerPlayer, int currentTurn, int evolutionPointCost,
            int firstPlayerEvolutionStartTurn, int secondPlayerEvolutionStartTurn)
        {
            // 检查是否已经进化
            if (IsEvolved)
                return false;

            // 检查EP是否足够
            if (ownerPlayer.CurrentEvolutionPoints < evolutionPointCost)
                return false;

            // 检查回合限制
            int evolutionStartTurn = ownerPlayer.IsFirst ? firstPlayerEvolutionStartTurn : secondPlayerEvolutionStartTurn;
            if (currentTurn < evolutionStartTurn)
                return false;

            return true;
        }

        /// <summary>
        /// 执行进化操作
        /// </summary>
        /// <param name="evolvedDefinition">进化后的卡牌定义</param>
        /// <returns>生成的事件列表</returns>
        public List<IGameEvent> Evolve(CardDefinition evolvedDefinition)
        {
            var events = new List<IGameEvent>();

            // 更新卡牌定义ID
            DefinitionId = evolvedDefinition.Id;

            // 更新基础属性
            BaseAttack = evolvedDefinition.Attack ?? 0;
            BaseHealth = evolvedDefinition.Health ?? 0;
            CurrentAttack = evolvedDefinition.Attack ?? 0;

            // 进化时生命值增加（Shadowverse规则：进化时生命值+2）
            var healthIncrease = (evolvedDefinition.Health ?? 0) - BaseHealth;
            MaxHealth = evolvedDefinition.Health ?? 0;
            CurrentHealth += healthIncrease; // 增加生命值而不是重置

            // 更新基础关键字
            if (evolvedDefinition.Keywords != null)
            {
                var evolvedKeywords = evolvedDefinition.Keywords
                    .Select(k => Enum.TryParse<CardKeyword>(k, true, out var kw) ? kw : CardKeyword.None)
                    .Where(k => k != CardKeyword.None);
                SetBaseKeywords(evolvedKeywords);
            }

            // 标记为已进化
            IsEvolved = true;

            // 进化后可以立即攻击（Shadowverse规则）
            CanAttack = true;

            // TODO: 生成进化事件
            // var evolveEvent = new CardEvolvedEvent(this.InstanceId, evolvedDefinition.Id);
            // events.Add(evolveEvent);

            return events;
        }

        /// <summary>
        /// 获取能够响应指定事件类型的能力
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <returns>能够响应该事件的能力列表</returns>
        public IEnumerable<Ability> GetAbilitiesTriggeredBy(GameEventType eventType)
        {
            return Abilities.Where(a => a.Trigger.EventTypes.Contains(eventType));
        }

        /// <summary>
        /// 获取当前激活的能力
        /// </summary>
        /// <returns>激活的能力列表</returns>
        public IEnumerable<Ability> GetActiveAbilities()
        {
            return Abilities.Where(a => a.IsActive);
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
            set => CurrentHealth = value;
        }

        // Helper methods for type checking
        public bool IsLeader => CardType.Equals("Leader", StringComparison.OrdinalIgnoreCase);
        public bool IsFollower => CardType.Equals("Follower", StringComparison.OrdinalIgnoreCase);
        public bool IsSpell => CardType.Equals("Spell", StringComparison.OrdinalIgnoreCase);
        public bool IsAmulet => CardType.Equals("Amulet", StringComparison.OrdinalIgnoreCase);

        // Helper methods for zone checking
        public bool IsInHand => CurrentZone == CardZone.Hand;
        public bool IsOnField => CurrentZone == CardZone.Field;
        public bool IsInDeck => CurrentZone == CardZone.Deck;
        public bool IsInGraveyard => CurrentZone == CardZone.Graveyard;

        // Keyword management methods
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
        /// 添加关键字（临时关键字，可以被移除）
        /// </summary>
        /// <param name="keyword">关键字枚举</param>
        /// <returns>如果成功添加则返回true</returns>
        public bool AddKeyword(CardKeyword keyword)
        {
            if (keyword == CardKeyword.None)
                return false;

            // 检查是否已经拥有该关键字
            if (HasKeyword(keyword))
                return false;

            // 添加到临时关键字列表
            TemporaryKeywords.Add(keyword);

            // 更新总关键字列表
            RefreshKeywords();

            return true;
        }

        /// <summary>
        /// 添加关键字（临时关键字，可以被移除）- 字符串版本，用于向后兼容
        /// </summary>
        /// <param name="keyword">关键字名称</param>
        /// <returns>如果成功添加则返回true</returns>
        public bool AddKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return false;

            // 尝试将字符串转换为CardKeyword枚举
            if (Enum.TryParse<CardKeyword>(keyword, true, out var keywordEnum))
            {
                return AddKeyword(keywordEnum);
            }

            return false;
        }

        /// <summary>
        /// 移除关键字（只能移除临时关键字）
        /// </summary>
        /// <param name="keyword">关键字枚举</param>
        /// <returns>如果成功移除则返回true</returns>
        public bool RemoveKeyword(CardKeyword keyword)
        {
            if (keyword == CardKeyword.None)
                return false;

            // 只能移除临时关键字
            var removed = TemporaryKeywords.Remove(keyword);

            if (removed)
            {
                // 更新总关键字列表
                RefreshKeywords();
            }

            return removed;
        }

        /// <summary>
        /// 移除关键字（只能移除临时关键字）- 字符串版本，用于向后兼容
        /// </summary>
        /// <param name="keyword">关键字名称</param>
        /// <returns>如果成功移除则返回true</returns>
        public bool RemoveKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return false;

            // 尝试将字符串转换为CardKeyword枚举
            if (Enum.TryParse<CardKeyword>(keyword, true, out var keywordEnum))
            {
                return RemoveKeyword(keywordEnum);
            }

            return false;
        }

        /// <summary>
        /// 设置基础关键字（来自卡牌定义）
        /// </summary>
        /// <param name="keywords">基础关键字列表</param>
        public void SetBaseKeywords(IEnumerable<CardKeyword> keywords)
        {
            BaseKeywords.Clear();
            if (keywords != null)
            {
                BaseKeywords.AddRange(keywords.Where(k => k != CardKeyword.None));
            }
            RefreshKeywords();
        }

        /// <summary>
        /// 设置基础关键字（来自卡牌定义）- 字符串版本，用于向后兼容
        /// </summary>
        /// <param name="keywords">基础关键字列表</param>
        public void SetBaseKeywords(IEnumerable<string> keywords)
        {
            BaseKeywords.Clear();
            if (keywords != null)
            {
                foreach (var keyword in keywords.Where(k => !string.IsNullOrWhiteSpace(k)))
                {
                    if (Enum.TryParse<CardKeyword>(keyword, true, out var keywordEnum) && keywordEnum != CardKeyword.None)
                    {
                        BaseKeywords.Add(keywordEnum);
                    }
                }
            }
            RefreshKeywords();
        }

        /// <summary>
        /// 清除所有临时关键字
        /// </summary>
        public void ClearTemporaryKeywords()
        {
            TemporaryKeywords.Clear();
            RefreshKeywords();
        }

        /// <summary>
        /// 刷新总关键字列表（合并基础关键字和临时关键字）
        /// </summary>
        private void RefreshKeywords()
        {
            Keywords.Clear();
            Keywords.AddRange(BaseKeywords);
            Keywords.AddRange(TemporaryKeywords);

            // 去重（保持顺序，基础关键字优先）
            Keywords = [.. Keywords.Distinct()];
        }

        // Leader-specific methods (only applicable when IsLeader is true)
        /// <summary>
        /// Applies damage to this card instance and returns generated events.
        /// </summary>
        /// <param name="amount">Amount of damage to apply</param>
        /// <param name="source">Source of the damage (optional)</param>
        /// <returns>List of events generated by taking damage</returns>
        public List<IGameEvent> TakeDamage(int amount, CardInstance? source = null)
        {
            var events = new List<IGameEvent>();

            if (amount < 0) return events;

            var actualDamage = Math.Min(amount, CurrentHealth);
            CurrentHealth -= actualDamage;

            // Generate damage event
            events.Add(new DamageEvent(this, actualDamage, source));

            if (CurrentHealth <= 0)
            {
                // Generate destruction event - this will be handled by the caller
                // who has access to PlayerStateDto
            }

            return events;
        }

        /// <summary>
        /// Applies healing to this card instance and returns generated events.
        /// </summary>
        /// <param name="amount">Amount of healing to apply</param>
        /// <param name="source">Source of the healing (optional)</param>
        /// <returns>List of events generated by healing</returns>
        public List<IGameEvent> Heal(int amount, CardInstance? source = null)
        {
            var events = new List<IGameEvent>();

            if (amount < 0 || CurrentHealth <= 0) return events;

            var maxPossibleHealing = MaxHealth - CurrentHealth;
            var actualHealing = Math.Min(amount, maxPossibleHealing);

            CurrentHealth += actualHealing;

            if (actualHealing > 0)
            {
                events.Add(new HealEvent(this, actualHealing, source));
            }

            return events;
        }

        // Amulet-specific methods (only applicable when IsAmulet is true)
        /// <summary>
        /// Decrements countdown and returns generated events.
        /// </summary>
        /// <returns>List of events generated by countdown tick</returns>
        public List<IGameEvent> TickCountdown()
        {
            var events = new List<IGameEvent>();

            if (IsAmulet && Countdown.HasValue)
            {
                Countdown--;
                if (Countdown <= 0)
                {
                    // Generate countdown finished event - this will be handled by the caller
                    // who has access to PlayerStateDto
                }
            }

            return events;
        }

        // === Buff-aware property calculation methods ===

        /// <summary>
        /// 计算考虑Buff效果的当前攻击力
        /// </summary>
        /// <param name="buffManager">Buff管理器实例</param>
        /// <param name="gameContext">游戏上下文（用于Buff条件评估，可选）</param>
        /// <param name="conditionEvaluator">条件评估器（用于Buff条件评估，可选）</param>
        /// <returns>计算后的攻击力值</returns>
        public async Task<int> GetEffectiveAttackAsync(IBuffManager buffManager,
            GameContext? gameContext = null, IConditionEvaluator? conditionEvaluator = null)
        {
            if (buffManager == null)
                return CurrentAttack; // Fallback to stored value if no BuffManager

            return await CalculateStatWithBuffsAsync(StatType.Attack, BaseAttack, buffManager, gameContext, conditionEvaluator);
        }

        /// <summary>
        /// 计算考虑Buff效果的当前最大生命值
        /// </summary>
        /// <param name="buffManager">Buff管理器实例</param>
        /// <param name="gameContext">游戏上下文（用于Buff条件评估，可选）</param>
        /// <param name="conditionEvaluator">条件评估器（用于Buff条件评估，可选）</param>
        /// <returns>计算后的最大生命值</returns>
        public async Task<int> GetEffectiveMaxHealthAsync(IBuffManager buffManager,
            GameContext? gameContext = null, IConditionEvaluator? conditionEvaluator = null)
        {
            if (buffManager == null)
                return MaxHealth; // Fallback to stored value if no BuffManager

            return await CalculateStatWithBuffsAsync(StatType.Defense, BaseHealth, buffManager, gameContext, conditionEvaluator);
        }

        /// <summary>
        /// 计算考虑Buff效果的当前费用
        /// </summary>
        /// <param name="buffManager">Buff管理器实例</param>
        /// <param name="gameContext">游戏上下文（用于Buff条件评估，可选）</param>
        /// <param name="conditionEvaluator">条件评估器（用于Buff条件评估，可选）</param>
        /// <returns>计算后的费用值</returns>
        public async Task<int> GetEffectiveCostAsync(IBuffManager buffManager,
            GameContext? gameContext = null, IConditionEvaluator? conditionEvaluator = null)
        {
            if (buffManager == null)
                return CurrentCost; // Fallback to stored value if no BuffManager

            return await CalculateStatWithBuffsAsync(StatType.Cost, BaseCost, buffManager, gameContext, conditionEvaluator);
        }

        /// <summary>
        /// 获取考虑Buff效果的所有关键字
        /// </summary>
        /// <param name="buffManager">Buff管理器实例</param>
        /// <param name="gameContext">游戏上下文（用于Buff条件评估，可选）</param>
        /// <param name="conditionEvaluator">条件评估器（用于Buff条件评估，可选）</param>
        /// <returns>包含所有有效关键字的列表</returns>
        public async Task<List<CardKeyword>> GetEffectiveKeywordsAsync(IBuffManager buffManager,
            GameContext? gameContext = null,
            IConditionEvaluator? conditionEvaluator = null)
        {
            // If silenced, return empty list (no keywords)
            if (IsSilenced)
            {
                return [];
            }

            var effectiveKeywords = new List<CardKeyword>(BaseKeywords);
            effectiveKeywords.AddRange(TemporaryKeywords);

            if (buffManager != null)
            {
                var buffs = await buffManager.GetBuffsForCardAsync(InstanceId);
                foreach (var buff in buffs)
                {
                    if (buff.IsActive)
                    {
                        // 评估Buff的生效条件
                        bool isApplicable = true;
                        if (gameContext != null && conditionEvaluator != null)
                        {
                            try
                            {
                                isApplicable = await buff.IsCurrentlyApplicableAsync(this, gameContext, conditionEvaluator);
                            }
                            catch (Exception)
                            {
                                // 如果条件评估失败，默认认为Buff不适用
                                isApplicable = false;
                            }
                        }

                        if (isApplicable)
                        {
                            var buffKeywords = buff.GetGrantedKeywords();
                            // Convert string keywords to CardKeyword enum
                            foreach (var keywordStr in buffKeywords)
                            {
                                if (Enum.TryParse<CardKeyword>(keywordStr, true, out var keywordEnum) && keywordEnum != CardKeyword.None)
                                {
                                    effectiveKeywords.Add(keywordEnum);
                                }
                            }
                        }
                    }
                }
            }

            // Remove duplicates while preserving order
            return [.. effectiveKeywords.Distinct()];
        }

        /// <summary>
        /// 核心方法：根据Buff效果计算属性值
        /// </summary>
        /// <param name="statType">属性类型</param>
        /// <param name="baseValue">基础值</param>
        /// <param name="buffManager">Buff管理器</param>
        /// <param name="gameContext">游戏上下文（用于Buff条件评估，可选）</param>
        /// <param name="conditionEvaluator">条件评估器（用于Buff条件评估，可选）</param>
        /// <returns>计算后的属性值</returns>
        private async Task<int> CalculateStatWithBuffsAsync(StatType statType, int baseValue, IBuffManager buffManager,
            GameContext? gameContext = null, IConditionEvaluator? conditionEvaluator = null)
        {
            var currentValue = baseValue;
            var buffs = await buffManager.GetBuffsForCardAsync(InstanceId);

            // Apply buffs in order of application (as stored in BuffManager)
            foreach (var buff in buffs)
            {
                if (!buff.IsActive)
                    continue;

                // 评估Buff的生效条件
                bool isApplicable = true;
                if (gameContext != null && conditionEvaluator != null)
                {
                    try
                    {
                        isApplicable = await buff.IsCurrentlyApplicableAsync(this, gameContext, conditionEvaluator);
                    }
                    catch (Exception)
                    {
                        // 如果条件评估失败，默认认为Buff不适用
                        isApplicable = false;
                    }
                }

                if (isApplicable)
                {
                    var statModifications = buff.GetStatModifications()
                        .Where(mod => mod.Stat == statType)
                        .OrderBy(mod => mod.Layer); // Apply in layer order

                    foreach (var modification in statModifications)
                    {
                        currentValue = ApplyStatModification(currentValue, modification);
                    }
                }
            }

            return Math.Max(0, currentValue); // Ensure non-negative values
        }

        /// <summary>
        /// 应用单个属性修改
        /// </summary>
        /// <param name="currentValue">当前值</param>
        /// <param name="modification">修改配置</param>
        /// <returns>修改后的值</returns>
        private static int ApplyStatModification(int currentValue, StatModification modification)
        {
            return modification.Layer switch
            {
                ModifierLayer.Base => modification.Value, // Replace base value (rarely used)
                ModifierLayer.Additive => currentValue + modification.Value, // Add/subtract
                ModifierLayer.Multiplicative => (int)(currentValue * (modification.Value / 100.0)), // Percentage modification
                ModifierLayer.Set => modification.Value, // Set to specific value
                _ => currentValue // Unknown layer, no change
            };
        }

        // Removed ToDto() method as mapping is now handled by AutoMapper (CardInstanceProfile)
    } // Closing brace for CardInstance class
} // Closing brace for namespace