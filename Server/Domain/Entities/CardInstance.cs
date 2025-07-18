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
using ShadowverseBeyond.Server.Domain.Constants; // For CardKeyword enum
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For TargetId
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // For DomainOperationResult

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
        /// The DefinitionId of the card this instance evolves into. Null if it cannot evolve.
        /// This is set from the CardDefinition's EvolveId upon creation.
        /// </summary>
        public int? EvolveDefinitionId { get; private set; }

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
        public CardType CardType { get; set; }

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
                _maxHealth = Math.Max(0, value); // MaxHealth cannot be negative.

                if (oldMaxHealth != _maxHealth)
                {
                    // This internal adjustment should not generate events directly.
                    // Events are generated by public methods like ModifyStats, TakeDamage, Heal.
                    AdjustCurrentHealthForMaxHealthChange(oldMaxHealth, _maxHealth);
                }
            }
        }

        /// <summary>
        /// Adjusts CurrentHealth when MaxHealth changes according to Shadowverse rules.
        /// This method purely adjusts _currentHealth based on MaxHealth changes, without event generation.
        /// </summary>
        /// <param name="oldMaxHealth">Previous maximum health value.</param>
        /// <param name="newMaxHealth">New maximum health value.</param>
        private void AdjustCurrentHealthForMaxHealthChange(int oldMaxHealth, int newMaxHealth)
        {
            if (oldMaxHealth == 0 && newMaxHealth > 0) // Initial setup when MaxHealth is first set from 0
            {
                _currentHealth = newMaxHealth;
            }
            else if (newMaxHealth > oldMaxHealth) // MaxHealth increased
            {
                var increase = newMaxHealth - oldMaxHealth;
                _currentHealth += increase; // CurrentHealth increases by the same amount as MaxHealth
            }
            // If MaxHealth decreased, CurrentHealth will be capped by its own setter's logic: Math.Min(value, MaxHealth)
            // or by direct setting if a ModifyStats method sets it.
            // We just need to ensure _currentHealth is not above the new _maxHealth.
            _currentHealth = Math.Min(_currentHealth, newMaxHealth);
            _currentHealth = Math.Max(0, _currentHealth); // And ensure it's not negative.
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
        /// Index of the card within its current zone's list (e.g., hand, field, deck).
        /// Null if not applicable or not in an ordered zone.
        /// </summary>
        public int? ZoneIndex { get; set; }

        /// <summary>
        /// Whether this card can attack this turn.
        /// </summary>
        public bool CanAttack { get; set; } = false;

        /// <summary>
        /// Whether this card is silenced (loses all abilities and keywords but retains stat modifications).
        /// </summary>
        public bool IsSilenced { get; private set; } = false;

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
        /// A private list of buffs currently applied to this card instance.
        /// Direct manipulation should be avoided; use domain methods like AddBuff and RemoveBuffs.
        /// </summary>
        private readonly List<BuffConfig> _buffs = new();

        /// <summary>
        /// Public read-only view of the buffs applied to this card instance.
        /// </summary>
        public IReadOnlyList<BuffConfig> Buffs => _buffs.AsReadOnly();

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
        [Obsolete("Card instances should be created with a pre-generated InstanceId, typically via ICardFactory.")]
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
        public bool CanPerformAttack(GameState gameState, PlayerState attackerPlayer, bool forceAttack = false)
        {
            if (forceAttack)
            {
                Console.WriteLine("[BUG-017_DEBUG] - Force attack enabled, returning true");
                return true;
            }

            // 游戏状态验证
            if (gameState.IsGameOver)
            {
                Console.WriteLine("[BUG-017_DEBUG] - Game is over, cannot attack");
                return false;
            }

            if (gameState.CurrentPlayerId != attackerPlayer.PlayerId)
            {
                Console.WriteLine($"[BUG-017_DEBUG] - Not attacker's turn. Current: {gameState.CurrentPlayerId}, Attacker: {attackerPlayer.PlayerId}");
                return false;
            }

            // 攻击者验证
            if (!CanAttack)
            {
                Console.WriteLine("[BUG-017_DEBUG] - CanAttack is false (summoning sickness or other restriction)");
                return false;
            }

            if (CurrentAttack <= 0)
            {
                Console.WriteLine($"[BUG-017_DEBUG] - CurrentAttack is {CurrentAttack}, cannot attack with 0 or negative attack");
                return false;
            }

            // 区域验证
            if (CurrentZone != CardZone.Field)
            {
                Console.WriteLine($"[BUG-017_DEBUG] - Card is not on field. Current zone: {CurrentZone}");
                return false;
            }

            // 检查是否已达到本回合最大攻击次数
            var maxAttacksPerTurn = GetMaxAttacksPerTurn();
            if (AttacksThisTurn >= maxAttacksPerTurn)
            {
                Console.WriteLine($"[BUG-017_DEBUG] - Already attacked {AttacksThisTurn}/{maxAttacksPerTurn} times this turn");
                return false;
            }

            Console.WriteLine("[BUG-017_DEBUG] - All attack preconditions passed, can attack");
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
        public DomainOperationResult PerformAttack(CardInstance target)
        {
            var result = new DomainOperationResult();

            // 记录攻击次数
            AttacksThisTurn++;

            // 检查是否已达到最大攻击次数，并更新可攻击状态
            if (AttacksThisTurn >= GetMaxAttacksPerTurn())
            {
                CanAttack = false;
                result.GeneratedEvents.Add(new AttackStateUpdatedEvent(this, this.CanAttack));
            }

            // 【BUG-020修复】保存目标的原始攻击力，因为伤害计算不应受死亡状态影响
            int targetCounterAttack = target.CurrentAttack;

            Console.WriteLine($"[BUG-020_DEBUG] {Name} (ATK: {CurrentAttack}) attacking {target.Name} (ATK: {targetCounterAttack}, HP: {target.CurrentHealth})");

            // 计算伤害
            int damage = CurrentAttack;

            // 对目标造成伤害
            var damageResult = target.TakeDamage(damage, this);
            result.GeneratedEvents.AddRange(damageResult.GeneratedEvents);
            // result.EffectRequests.AddRange(damageResult.EffectRequests); // Removed: Effects are created by Abilities

            Console.WriteLine($"[BUG-020_DEBUG] After damage to target: {target.Name} HP: {target.CurrentHealth}");

            // 只要目标是随从，就应该反击
            if (target.CardType == CardType.Follower)
            {
                Console.WriteLine($"[BUG-020_DEBUG] Target {target.Name} counter-attacking for {targetCounterAttack} damage");
                var counterAttackResult = TakeDamage(targetCounterAttack, target);
                result.GeneratedEvents.AddRange(counterAttackResult.GeneratedEvents);
                // result.EffectRequests.AddRange(counterAttackResult.EffectRequests); // Removed: Effects are created by Abilities

                Console.WriteLine($"[BUG-020_DEBUG] After counter-attack: {Name} HP: {CurrentHealth}");
            }
            else
            {
                Console.WriteLine($"[BUG-020_DEBUG] No counter-attack: targetAttack={targetCounterAttack}, cardType={target.CardType}");
            }

            // 生成攻击事件
            result.GeneratedEvents.Add(new AttackEvent(this, TargetInfo.ForCard(target)));
            result.Success = true; // Assuming attack itself is successful if it reaches this point; damage/destruction are outcomes.
            return result;
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
        public bool CanEvolve()
        {
            // This method now only checks for intrinsic properties of the card itself.
            // All game-rule checks (EP cost, turn number, player's evolution right)
            // are handled by the calling Ability (e.g., EvolveAbility).
            return !IsEvolved && EvolveDefinitionId.HasValue;
        }

        /// <summary>
        /// Applies evolution to this card instance using a new card definition.
        /// This method performs a "transformation" rather than just buffing stats.
        /// </summary>
        /// <param name="evolvedDefinition">The definition of the card to evolve into.</param>
        /// <returns>A result object containing generated events.</returns>
        public DomainOperationResult ApplyEvolution(CardDefinition evolvedDefinition)
        {
            var result = new DomainOperationResult();

            // Use the existing initialization logic to transform the card
            InitializeFromDefinition(evolvedDefinition);

            // Set evolution-specific state
            IsEvolved = true;
            CanAttack = true; // Evolved units can attack immediately (unless they have an effect preventing it)

            // Generate the evolution event
            result.GeneratedEvents.Add(new EvolveEvent(this));
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
        public bool IsLeader => CardType == CardType.Leader;
        public bool IsFollower => CardType == CardType.Follower;
        public bool IsSpell => CardType == CardType.Spell;
        public bool IsAmulet => CardType == CardType.Amulet;

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

        /// <summary>
        /// 添加关键字（临时关键字，可以被移除）- 字符串版本，用于向后兼容
        /// </summary>
        /// <param name="keyword">关键字名称</param>
        /// <returns>如果成功添加则返回true</returns>
        public DomainOperationResult AddKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new DomainOperationResult { Success = false, ErrorMessage = "Keyword cannot be empty." };

            // 尝试将字符串转换为CardKeyword枚举
            if (Enum.TryParse<CardKeyword>(keyword, true, out var keywordEnum))
            {
                return AddKeyword(keywordEnum);
            }

            return new DomainOperationResult { Success = false, ErrorMessage = $"Invalid keyword: {keyword}" };
        }

        /// <summary>
        /// 【领域方法】为卡牌添加一个关键字，并生成相应的领域事件。
        /// </summary>
        /// <param name="keyword">要添加的关键字。</param>
        /// <returns>包含操作结果的DomainOperationResult。</returns>
        public DomainOperationResult AddKeyword(CardKeyword keyword)
        {
            var result = new DomainOperationResult();

            if (keyword == CardKeyword.None)
            {
                result.Success = false;
                result.ErrorMessage = "Cannot add 'None' as a keyword.";
                return result;
            }
            if (HasKeyword(keyword))
            {
                result.Success = false;
                result.ErrorMessage = $"Card already has keyword '{keyword}'.";
                return result;
            }

            TemporaryKeywords.Add(keyword);
            RefreshKeywords();
            result.Success = true;
            result.GeneratedEvents.Add(new KeywordAddedEvent(keyword, this));

            return result;
        }

        /// <summary>
        /// 移除关键字（只能移除临时关键字）
        /// </summary>
        /// <param name="keyword">关键字枚举</param>
        /// <returns>如果成功移除则返回true</returns>

        /// <summary>
        /// 移除关键字（只能移除临时关键字）- 字符串版本，用于向后兼容
        /// </summary>
        /// <param name="keyword">关键字名称</param>
        /// <returns>如果成功移除则返回true</returns>
        public DomainOperationResult RemoveKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new DomainOperationResult { Success = false, ErrorMessage = "Keyword cannot be empty." };

            // 尝试将字符串转换为CardKeyword枚举
            if (Enum.TryParse<CardKeyword>(keyword, true, out var keywordEnum))
            {
                return RemoveKeyword(keywordEnum);
            }

            return new DomainOperationResult { Success = false, ErrorMessage = $"Invalid keyword: {keyword}" };
        }
        /// <summary>
        /// 【领域方法】从此卡牌实例中移除一个关键字，并生成相应的领域事件。
        /// </summary>
        /// <param name="keyword">要移除的关键字。</param>
        /// <returns>包含操作结果的DomainOperationResult。</returns>
        public DomainOperationResult RemoveKeyword(CardKeyword keyword)
        {
            var result = new DomainOperationResult();

            if (keyword == CardKeyword.None)
            {
                result.Success = false;
                result.ErrorMessage = "Cannot remove 'None' as a keyword.";
                return result;
            }

            if (!TemporaryKeywords.Remove(keyword))
            {
                result.Success = false;
                result.ErrorMessage = $"Card does not have temporary keyword '{keyword}' or it cannot be removed.";
                return result;
            }

            RefreshKeywords();
            result.Success = true;
            result.GeneratedEvents.Add(new KeywordRemovedEvent(keyword, this));

            return result;
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
        /// Internal method to apply actual health changes and generate HealthChangedEvent.
        /// </summary>
        private DomainOperationResult ApplyHealthChange(int changeAmount, EventType reasonEvent, CardInstance? sourceCardContext)
        {
            var result = new DomainOperationResult();
            // changeAmount is positive for heal, negative for damage.
            if (changeAmount == 0 && reasonEvent != EventType.StatChanged) // StatChanged might set health to a new value even if delta is 0
            {
                result.Success = true;
                return result;
            }

            int oldHealth = this.CurrentHealth;
            int newHealthCalculated;

            if (changeAmount < 0) // Damage
            {
                newHealthCalculated = Math.Max(0, this.CurrentHealth + changeAmount);
            }
            else // Healing or direct set (if reasonEvent is StatChanged)
            {
                newHealthCalculated = Math.Min(this.MaxHealth, this.CurrentHealth + changeAmount);
            }

            // If reason is StatChanged, it might be a direct set, not a delta.
            // However, the HealthChangedEvent expects a 'changeAmount' that reflects the delta.
            // For simplicity, this method assumes 'changeAmount' IS the delta.
            // If it's a direct set (e.g. "Set Health to X"), the caller of ApplyHealthChange
            // should calculate the delta first.

            if (oldHealth != newHealthCalculated)
            {
                this.CurrentHealth = newHealthCalculated; // The setter handles MaxHealth capping.
                // The 'changeAmount' for HealthChangedEvent should reflect the actual change.
                int actualDelta = this.CurrentHealth - oldHealth;
                if (sourceCardContext != null)
                {
                    result.GeneratedEvents.Add(new HealthChangedEvent(this, oldHealth, this.CurrentHealth, this.MaxHealth, actualDelta, sourceCardContext));
                }
            }
            result.Success = true;
            return result;
        }

        /// <summary>
        /// Applies damage to this card instance and returns generated events.
        /// </summary>
        /// <param name="amount">Amount of damage to apply (should be positive).</param>
        /// <param name="source">Source of the damage (optional).</param>
        /// <returns>DomainOperationResult containing DamageEvent and potentially HealthChangedEvent and DestroyEffectRequest.</returns>
        public DomainOperationResult TakeDamage(int amount, CardInstance? source = null)
        {
            var result = new DomainOperationResult();

            if (amount <= 0)
            {
                result.Success = true; // Taking 0 or negative damage is not an error.
                return result;
            }

            // Record the intent to damage with the original requested amount
            if (source != null)
            {
                result.GeneratedEvents.Add(new DamageEvent(this, amount, source));
            }

            // Apply the actual health change
            // actualDamageDealt will be negative for the ApplyHealthChange method
            int actualDamageDealt = Math.Min(amount, this.CurrentHealth); // Positive value for how much health will be lost
            var healthChangeResult = ApplyHealthChange(-actualDamageDealt, EventType.Damage, source);
            result.GeneratedEvents.AddRange(healthChangeResult.GeneratedEvents);

            // The handler is now responsible for checking health and triggering destroy.
            // This method's responsibility ends here.

            result.Success = true; // Ensure success is true if damage process completes
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
            var result = new DomainOperationResult();

            if (amount <= 0 || this.CurrentHealth <= 0 || this.CurrentHealth >= this.MaxHealth)
            {
                result.Success = true; // Not an error if no healing is applicable.
                return result;
            }

            // Record the intent to heal with the original requested amount
            if (source != null)
            {
                result.GeneratedEvents.Add(new HealEvent(this, amount, source));
            }

            // Apply the actual health change
            // actualHealingAmount will be positive for ApplyHealthChange
            int actualHealingAmount = Math.Min(amount, this.MaxHealth - this.CurrentHealth);
            if (actualHealingAmount > 0) // Ensure we only call ApplyHealthChange if there's actual healing to do
            {
                var healthChangeResult = ApplyHealthChange(actualHealingAmount, EventType.Heal, source);
                result.GeneratedEvents.AddRange(healthChangeResult.GeneratedEvents);

            }
            // CurrentHealth is now updated via ApplyHealthChange

            result.Success = true;
            return result;
        }


        /// <summary>
        /// 【领域方法】将此卡牌实例变形为另一个卡牌定义。
        /// </summary>
        /// <param name="newDefinition">新的卡牌定义。</param>
        /// <returns>包含操作结果的DomainOperationResult。</returns>
        public DomainOperationResult Transform(CardDefinition newDefinition)
        {
            var result = new DomainOperationResult();

            // 【修复】完整地从CardDefinition初始化所有属性
            InitializeFromDefinition(newDefinition);

            result.GeneratedEvents.Add(new TransformEvent(this));
            result.Success = true;

            return result;
        }
        /// <summary>
        /// 【领域方法】从此卡牌实例中移除一个或多个Buff，并生成相应的领域事件。
        /// </summary>
        /// <param name="parameters">定义要移除的Buff的参数。</param>
        /// <param name="buffManager">用于执行Buff操作的管理器。</param>
        /// <returns>包含操作结果和生成事件的DomainOperationResult。</returns>
        public DomainOperationResult RemoveBuffs(RemoveBuffEffectParams parameters, IBuffManager buffManager)
        {
            var result = new DomainOperationResult { Success = true };

            var buffsMatchingPredicate = buffManager.GetBuffsForCard(this.InstanceId)
                                    .Where(b => string.IsNullOrEmpty(parameters.BuffName) || b.DefinitionId == parameters.BuffName)
                                    .ToList();

            var buffsToActuallyRemove = parameters.RemoveCount > 0
                ? buffsMatchingPredicate.Take(parameters.RemoveCount).ToList()
                : buffsMatchingPredicate;

            if (!buffsToActuallyRemove.Any())
            {
                return result; // No buffs to remove
            }

            var specificBuffIds = new HashSet<Guid>(buffsToActuallyRemove.Select(b => b.Id));
            Func<BuffConfig, bool> finalPredicate = b => specificBuffIds.Contains(b.Id);

            var removalResult = buffManager.RemoveAllBuffs(this.InstanceId, finalPredicate);

            // The BuffManager is now responsible for generating the BuffRemovedEvents.
            // We just merge its results into our own.
            result.GeneratedEvents.AddRange(removalResult.GeneratedEvents);
            result.Success = removalResult.Success;
            result.ErrorMessage = removalResult.ErrorMessage;

            return result;
        }
        /// <summary>
        /// 【领域方法】向此卡牌实例添加一个已创建的Buff，并生成相应的领域事件。
        /// </summary>
        /// <param name="buffManager">用于注册Buff的管理器。</param>
        /// <param name="buffToAdd">要添加的已配置好的Buff实例。</param>
        /// <returns>包含操作结果和生成事件的DomainOperationResult。</returns>
        public DomainOperationResult AddBuff(IBuffManager buffManager, BuffConfig buffToAdd)
        {
            var result = new DomainOperationResult { Success = true };

            if (buffToAdd == null)
            {
                result.Success = false;
                result.ErrorMessage = "Cannot add a null buff.";
                return result;
            }

            var registrationResult = buffManager.RegisterBuff(buffToAdd);

            result.GeneratedEvents.AddRange(registrationResult.GeneratedEvents);
            result.Success = registrationResult.Success;
            result.ErrorMessage = registrationResult.ErrorMessage;

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
        /// </summary>
        /// <param name="definition">卡牌定义</param>
        public void InitializeFromDefinition(CardDefinition definition)
        {
            this.DefinitionId = definition.Id;
            this.EvolveDefinitionId = definition.EvolveId; // Set the EvolveDefinitionId from the definition
            this.BaseAttack = definition.Attack ?? 0;
            this.BaseHealth = definition.Health ?? 0;
            this.CurrentAttack = definition.Attack ?? 0;
            this.MaxHealth = definition.Health ?? 0;
            this.CurrentHealth = definition.Health ?? 0; // Set CurrentHealth after MaxHealth
            this.CardType = definition.Type;
            this.BaseCost = definition.Cost;
            this.CurrentCost = definition.Cost;
            this.IsEvolved = false;
            this.Countdown = definition.Type == CardType.Amulet ? definition.Health : null;

            // 【关键修复】设置显示属性
            this.Name = definition.Name;
            this.Description = definition.Description ?? string.Empty;
            this.Rarity = definition.Rarity ?? string.Empty;
            this.Class = definition.Class ?? string.Empty;
            this.Tribe = definition.Tribe;

            this.SetBaseKeywords(definition.Keywords?.Select(k => Enum.TryParse<CardKeyword>(k, true, out var kw) ? kw : CardKeyword.None).Where(k => k != CardKeyword.None) ?? Enumerable.Empty<CardKeyword>());
            this.ClearTemporaryKeywords();
        }

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