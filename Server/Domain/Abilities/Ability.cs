//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
//
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Exceptions;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// 运行时卡牌能力表示。
    /// 【架构最终版】职责：作为效果链（Effect Chain）的模板和工厂。
    /// </summary>
    public class Ability : IAbility
    {
        /// <summary>
        /// 静态配置引用
        /// </summary>
        public AbilityConfig Config { get; }

        // RuntimeId is part of IAbility
        public Guid RuntimeId { get; } // Already exists, ensure it's initialized (done in constructor)

        /// <summary>
        /// Gets the definition ID of the ability, linking to its static configuration.
        /// </summary>
        public long DefinitionId => Config.Id; // Implement DefinitionId

        /// <summary>
        /// 能力名称
        /// </summary>
        public string Name => Config.Name;

        /// <summary>
        /// 能力描述
        /// </summary>
        public string Description => Config.Description;

        /// <summary>
        /// 能力标签，用于标识能力类型（如"Fanfare", "LastWords", "Rush"等）
        /// </summary>
        public EffectKeyword AbilityTag => Config.AbilityTag;

        /// <summary>
        /// 能力类型
        /// </summary>
        public AbilityType Type => Config.Type;

        /// <summary>
        /// Gets the configuration for how this ability is triggered.
        /// This now directly implements the IAbility interface member.
        /// </summary>
        public TriggerConfig TriggerConfig => Trigger.Config; // Already matches IAbility

        /// <summary>
        /// 运行时触发器
        /// </summary>
        public Trigger Trigger { get; }

        /// <summary>
        /// 运行时条件列表
        /// </summary>
        public List<Condition> Conditions { get; }

        /// <summary>
        /// 运行时目标列表
        /// </summary>
        public List<Target> Targets { get; }

        /// <summary>
        /// 【废弃】不再使用运行时的Effect包装类。
        /// public List<Effect> Effects { get; }
        /// </summary>

        /// <summary>
        /// Gets the ID of the card instance that is the source of this ability, if applicable.
        /// Null if the ability is not sourced from a specific card (e.g., system abilities).
        /// </summary>
        public Guid? SourceCardId => SourceCard?.InstanceId;

        /// <summary>
        /// Gets the ID of the buff instance that is the source of this ability, if applicable.
        /// Null if the ability is not granted by a buff.
        /// </summary>
        public Guid? SourceBuffId { get; private set; }

        /// <summary>
        /// Gets the card instance that is the source of this ability by looking it up in the current game context.
        /// This is a computed property and may be null if the card is not found or if SourceCardId is null.
        /// </summary>
        public CardInstance? SourceCard { get; }

        /// <summary>
        /// 总触发次数
        /// </summary>
        public int TotalTriggerCount { get; private set; }

        /// <summary>
        /// 上次触发时间
        /// </summary>
        public DateTime? LastTriggerTime { get; private set; }

        private readonly IConditionFactory _conditionFactory;
        private readonly ITargetFactory _targetFactory;
        private readonly TargetSelectionService _targetSelectionService;

        private readonly Dictionary<(Guid TriggerEventId, int EffectIndex), ChooseDecisionState> _chooseDecisions = new();

        private sealed class ChooseDecisionState
        {
            public Dictionary<Guid, AbilityConfig> OptionsById { get; } = new();
            public Guid? SelectedOptionId { get; set; }
        }

        /// <summary>
        /// 构造函数，从AbilityConfig创建运行时Ability
        /// </summary>
        /// <param name="config">静态配置</param>
        /// <param name="conditionFactory">用于创建 Condition 实例的工厂</param>
        /// <param name="targetFactory">用于创建 Target 实例的工厂</param>
        /// <param name="targetSelectionService">用于解析和验证玩家目标选择的服务</param>
        /// <param name="sourceCard">源卡牌实例</param>
        public Ability(
            AbilityConfig config,
            IConditionFactory conditionFactory,
            ITargetFactory targetFactory,
            TargetSelectionService targetSelectionService,
            CardInstance? sourceCard = null,
            Guid? sourceBuffId = null)
        {
            RuntimeId = Guid.NewGuid(); // Initialize RuntimeId here as it's part of the interface
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _conditionFactory = conditionFactory ?? throw new ArgumentNullException(nameof(conditionFactory));
            _targetFactory = targetFactory ?? throw new ArgumentNullException(nameof(targetFactory));
            _targetSelectionService = targetSelectionService ?? throw new ArgumentNullException(nameof(targetSelectionService));
            SourceCard = sourceCard;
            SourceBuffId = sourceBuffId;
            // State is initialized by its property declaration.

            // 创建运行时组件
            Trigger = new Trigger(config.Trigger);

            Conditions = config.Conditions
                .Select(c => _conditionFactory.CreateCondition(c))
                .ToList();

            Targets = config.Targets
                .Select(kvp => _targetFactory.Create(kvp.Key, kvp.Value))
                .ToList();
        }

        /// <summary>
        /// 检查能力是否可以触发（包含区域检查优化）
        /// </summary>
        public Task<bool> CanTrigger(IGameEvent gameEvent, GameContext context)
        {
            // 移除 IsActive 和 State 检查
            // 激活状态由 AbilityManager 根据游戏上下文（如卡牌区域、沉默状态等）判断
            // 此处主要依赖 TriggerConfig 进行事件类型和区域匹配
            // 以及其他必要的上下文检查（例如，SourceCard 是否被沉默等，如果这些检查适合放在 CanTrigger）
            if (SourceCard != null && SourceCard.IsSilenced)
            {
                return Task.FromResult(false); // 被沉默的卡牌能力不能触发
            }
            return Task.FromResult(Trigger.CanTrigger(gameEvent, SourceCard));
        }

        /// <summary>
        /// 检查所有条件是否满足
        /// </summary>
        public bool CheckConditions(GameContext context)
        {
            foreach (var condition in Conditions)
            {
                if (!condition.Evaluate(context))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 解析所有目标，遵循IAbility接口的新契约。
        /// 此方法现在负责收集并返回所有解析到的目标。
        /// </summary>
        public Task<TargetResolutionResult> ResolveTargetsAsync(GameContext context, bool isCancellable = false)
        {
            // The entire logic of target resolution, including handling player choices,
            // is now delegated to the TargetSelectionService.
            // The service will now directly modify the state of the Target objects within this Ability instance,
            // leveraging their internal state management (NeedsResolution) instead of passing around a partialResult.
            return _targetSelectionService.ResolveTargetsForAbilityAsync(this, context, isCancellable);
        }

        /// <summary>
        /// Performs a non-blocking check to determine if all targets requiring a valid target can be met.
        /// This is used for playability checks.
        /// </summary>
        public Task<TargetAvailabilityResult> CheckTargetAvailabilityAsync(GameContext context)
        {
            return _targetSelectionService.CheckTargetAvailabilityAsync(this, context);
        }

        /// <summary>
        /// 【架构最终版】根据预先解析好的目标创建效果列表。
        /// 此方法现在是纯粹的“效果工厂”，不再包含条件检查或目标解析逻辑。
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <param name="resolvedTargets">由外部调用者（如EventProcessor）传入的、已经解析完毕的目标列表</param>
        /// <returns>一个包含所有待执行效果的列表</returns>
        public async Task<List<Effect>> CreateEffectsAsync(GameContext context, TargetResolutionResult targetResult)
        {
            var resultingEffects = new List<Effect>();

            // 根据配置，为每个EffectConfig创建一个独立的Effect请求
            var sortedEffectConfigs = Config.Effects
                .OrderBy(e => e.ExecutionOrder)
                .ToList();

            // 从上下文中获取IAbilityFactory，用于创建连锁能力
            var abilityFactory = context.ServiceProvider.GetRequiredService<IAbilityFactory>();
            var pendingActionService = context.ServiceProvider.GetRequiredService<IPendingActionService>();
            var triggeringEventId = context.TriggeringEvent?.EventId ?? Guid.Empty;

            for (var effectIndex = 0; effectIndex < sortedEffectConfigs.Count; effectIndex++)
            {
                var effectConfig = sortedEffectConfigs[effectIndex];
                List<TargetInfo> effectSpecificTargets;

                if (string.IsNullOrEmpty(effectConfig.TargetSelector))
                {
                    // 如果效果没有指定自己的目标选择器，则默认使用能力的所有已解析目标。
                    effectSpecificTargets = targetResult.ResolvedTargets.Values.SelectMany(list => list).ToList();
                }
                else
                {
                    if (!EffectTargetSelector.TrySelectTargets(effectConfig.TargetSelector, targetResult.ResolvedTargets, out effectSpecificTargets))
                    {
                        // Fallback: if the selector doesn't match any key, do not silently drop targets.
                        effectSpecificTargets = targetResult.ResolvedTargets.Values.SelectMany(list => list).ToList();
                    }
                }

                // --- Choose Branch Selection (pre-effect execution) ---
                // Choose is modeled as a no-op effect that only triggers ONE chained branch ability.
                // The actual player interaction is performed here (async), so we never block a sync effect handler.
                List<Ability>? overriddenChainedAbilities = null;
                if (effectConfig.Type == EffectType.Choose)
                {
                    overriddenChainedAbilities = await ResolveChooseBranchAbilitiesAsync(
                        context,
                        pendingActionService,
                        abilityFactory,
                        triggeringEventId,
                        effectIndex,
                        effectConfig);
                }

                var effectRequest = new Effect(
                    effectConfig,
                    context.RequestingPlayerId,
                    effectSpecificTargets, // 使用为该效果精确选择的目标
                    this.RuntimeId, // parentAbilityId
                    context.TriggeringEvent?.EventId ?? Guid.Empty, // triggeredEventId
                    this.SourceCard?.InstanceId // sourceCardId
                );

                context.CurrentGameState.InGameIds.GetOrCreate(InGameIdKind.Effect, effectRequest.Id);

                // --- 核心修改：生成时填充能力链 ---
                if (overriddenChainedAbilities != null)
                {
                    effectRequest.ChainedAbilities.AddRange(overriddenChainedAbilities);
                }
                else if (effectConfig.ChainedAbilities.Any())
                {
                    // 使用批量创建方法，虽然当前场景下差别不大，但更符合API设计
                    var chainedAbilities = abilityFactory.CreateAbilities(this.SourceCard, effectConfig.ChainedAbilities);
                    effectRequest.ChainedAbilities.AddRange(chainedAbilities);
                }
                // --- 修改结束 ---

                resultingEffects.Add(effectRequest);
            }

            // Cleanup: after the action is locked-in (commit phase), discard cached decisions for this trigger.
            if (!context.IsCancellable && triggeringEventId != Guid.Empty)
            {
                var keysToRemove = _chooseDecisions.Keys.Where(k => k.TriggerEventId == triggeringEventId).ToList();
                foreach (var key in keysToRemove)
                {
                    _chooseDecisions.Remove(key);
                }
            }

            return resultingEffects;
        }

        private async Task<List<Ability>> ResolveChooseBranchAbilitiesAsync(
            GameContext context,
            IPendingActionService pendingActionService,
            IAbilityFactory abilityFactory,
            Guid triggeringEventId,
            int effectIndex,
            EffectConfig effectConfig)
        {
            if (!effectConfig.ChainedAbilities.Any())
            {
                throw new GameLogicException("Choose effect requires at least one branch ability.");
            }

            var key = (triggeringEventId, effectIndex);
            if (!_chooseDecisions.TryGetValue(key, out var state))
            {
                state = new ChooseDecisionState();
                foreach (var branch in effectConfig.ChainedAbilities)
                {
                    state.OptionsById[Guid.NewGuid()] = branch;
                }
                _chooseDecisions[key] = state;
            }

            if (state.SelectedOptionId == null)
            {
                var optionIds = state.OptionsById.Keys.ToList();
                var optionCards = state.OptionsById.Select(kvp =>
                {
                    var optionId = kvp.Key;
                    var branch = kvp.Value;

                    return new CardInstanceDto
                    {
                        InstanceId = optionId,
                        CardId = 0,
                        Name = string.IsNullOrWhiteSpace(branch.Name) ? branch.AbilityTag.ToString() : branch.Name,
                        Cost = 0,
                        Type = CardType.Spell,
                        Rarity = CardRarity.Special,
                        Class = CardClass.Neutral,
                        Tribe = new List<string>(),
                        Attack = 0,
                        Health = 0,
                        CurrentAttack = 0,
                        CurrentHealth = 0,
                        MaxHealth = 0,
                        Description = branch.Description ?? string.Empty,
                        CanEvolve = false,
                        IsEvolved = false,
                        CanAttack = false,
                        AttacksThisTurn = 0,
                        FieldTurn = 0,
                        MaxAttack = 0,
                        Zone = CardZone.Extra,
                        Keywords = new List<CardKeyword>(),
                        EffectDescriptions = new List<string>(),
                        CurrentCountdown = null,
                        MaxAttacksPerTurn = 1,
                        CurrentCost = 0,
                        ZoneIndex = null,
                        OwnerPlayerId = context.RequestingPlayerId,
                        EvolvedState = null
                    };
                }).ToList();

                var request = new PlayerChoiceRequest
                {
                    PendingChoiceId = Guid.NewGuid(),
                    GameId = context.CurrentGameState.GameId,
                    TargetKey = $"choose_{effectIndex}",
                    SourceAbilityRuntimeId = this.RuntimeId,
                    RequestingPlayerId = context.RequestingPlayerId,
                    Message = "请选择一个选项",
                    SelectableTargetIds = optionIds,
                    SelectableCardInstances = optionCards,
                    MinSelectionCount = 1,
                    MaxSelectionCount = 1,
                    IsCancellable = context.IsCancellable
                };

                var result = await pendingActionService.WaitForPlayerChoiceAsync(context.CurrentGameState.GameId, request, context.CancellationToken);
                if (result.WasCancelled)
                {
                    throw new GameLogicException("Player cancelled Choose selection.");
                }

                var chosen = result.ChosenTargetIds.FirstOrDefault();
                if (chosen == Guid.Empty || !state.OptionsById.ContainsKey(chosen))
                {
                    throw new GameLogicException("Invalid Choose selection result.");
                }

                state.SelectedOptionId = chosen;
            }

            var selectedConfig = state.OptionsById[state.SelectedOptionId.Value];
            if (this.SourceCard == null)
            {
                throw new GameLogicException("Choose effect requires a source card to execute branch abilities.");
            }

            return abilityFactory.CreateAbilities(this.SourceCard, new[] { selectedConfig }, this.SourceBuffId);
        }

        /// <summary>
        /// 转换CardInstance列表到TargetId列表
        /// </summary>
        private static List<TargetId> ConvertTargetsToTargetIds(List<CardInstance> targets)
        {
            return targets.Select(t => TargetId.Card(t.InstanceId)).ToList();
        }

        /// <summary>
        /// 记录触发
        /// </summary>
        public void RecordTrigger(GameContext context)
        {
            TotalTriggerCount++;
            LastTriggerTime = DateTime.UtcNow;
            Trigger.RecordTrigger();
        }

        /// <summary>
        /// 重置回合状态
        /// </summary>
        public void ResetTurnState(GameContext context)
        {
            Trigger.ResetTurnCounters();
            Trigger.ResetEventFlags();

            foreach (var condition in Conditions)
                condition.ClearCache();

            foreach (var target in Targets)
                target.MarkForResolution();
        }

        /// <summary>
        /// 更新运行时状态
        /// </summary>
        public void UpdateRuntimeState(GameContext context)
        {
            foreach (var target in Targets)
            {
                if (!target.AreTargetsValid(context))
                    target.MarkForResolution();
            }
        }

        // Implement missing interface methods for trigger counts
        /// <summary>
        /// Gets the number of times this ability has been triggered during the current turn.
        /// </summary>
        public int GetTriggerCountThisTurn(GameContext context)
        {
            return Trigger.CurrentTurnTriggerCount;
        }

        /// <summary>
        /// Gets the total number of times this ability has been triggered during the current game.
        /// </summary>
        public int GetTriggerCountThisGame(GameContext context)
        {
            return TotalTriggerCount;
        }
    }
}
