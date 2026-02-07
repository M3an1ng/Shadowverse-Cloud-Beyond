# Design: Player-Facing Event/Effect Categories

## Context
Ability Editor 当前直接展示事件与效果枚举，ActionRequests（请求类事件）与系统事件混杂。玩家在编辑卡牌时通常以“能做什么”和“何时触发”为核心心智模型，需要玩家视角的分组与隐藏策略。

## Goals / Non-Goals
- Goals:
  - 为事件/效果元数据增加“玩家视角分类”与“可见性”信息，支持 UI 分组与折叠。
  - 默认隐藏 ActionRequests 与系统事件，避免误用。
  - 保持分类稳定、可扩展，不影响现有事件/效果语义。
- Non-Goals:
  - 不修改事件/效果的业务行为，仅调整元数据与展示。
  - 不对未暴露元数据的事件做强制补齐（可选任务）。

## Decisions
- 新增分类字段（CategoryId/CategoryLabel）与可见性字段（Visibility），由后端元数据统一提供。
- ActionRequests 事件设置 Visibility = System（或 Hidden），前端默认不显示，仅在“显示系统事件”开关开启后显示。
- AbilityTriggered 单独归类为“特殊事件”。

## Category Model
- CategoryId: ASCII 稳定标识（例如 `base-action`）
- CategoryLabel: 中文显示名（例如 `基础行动`）
- Visibility: `Visible` | `System` | `Hidden`

## Category Definitions (Player View)
1. `base-action` / 基础行动
   - 描述: 玩家操作直接导致或强可感知的行动与战斗结算链。
2. `position-change` / 位置变化
   - 描述: 卡牌登场、离场或跨区域移动。
3. `temp-state` / 临时状态
   - 描述: Buff、关键字、沉默等持续状态变化。
4. `resource-value` / 资源数值
   - 描述: PP/EP/SEP、属性、生命值等数值变化。
5. `turn-flow` / 回合流程
   - 描述: 回合开始/结束、倒计时等流程驱动事件。
6. `special` / 特殊事件
   - 描述: 能力系统内部触发类事件。
7. `system-request` / 系统请求
   - 描述: ActionRequests 与系统内部流程事件，默认隐藏。

## Event Category Mapping (Current Metadata)
- base-action: `攻击时`(AttackEvent), `战斗冲突`(Clash), `造成伤害`(DamageEvent), `治疗`(HealEvent), `进化`(EvolveEvent), `超进化`(SuperEvolveEvent), `出牌`(PlayCardEvent), `Act`(ActInvokedEvent - 若补齐元数据)
- position-change: `召唤`(SummonEvent), `创建卡牌`(CreateCardEvent), `卡牌移动`(CardMovedEvent), `卡牌公开`(CardRevealedEvent), `添加卡牌到手牌`(AddCardToHandEvent), `抽牌`(DrawEvent), `卡牌弃置`(CardDiscardedEvent), `卡牌返回`(ReturnEvent), `卡牌消失`(CardVanishedEvent), `摧毁卡牌`(DestroyEvent), `卡牌变形`(TransformEvent)
- temp-state: `Buff应用`(BuffAppliedEvent), `Buff移除`(BuffRemovedEvent), `关键字添加`(KeywordAddedEvent), `关键字移除`(KeywordRemovedEvent), `沉默应用`(SilenceAppliedEvent), `沉默移除`(SilenceRemovedEvent)
- resource-value: `PP更新`(PpUpdateEvent), `PP最大值更新`(PpMaxUpdateEvent), `EP更新`(EpUpdateEvent), `EP最大值更新`(EpMaxUpdateEvent), `SEP Update`(SepUpdateEvent), `SEP Max Update`(SepMaxUpdateEvent), `属性变化`(StatChangedEvent), `生命值变化`(HealthChangedEvent), `手牌更新`(HandUpdated)
- turn-flow: `回合开始时`(TurnStartEvent), `回合结束时`(TurnEndEvent), `倒计时减少`(CountdownTickEvent), `倒计时结束`(CountdownFinishedEvent), `换牌完成`(MulliganCompletedEvent), `游戏结束`(GameEndEvent)
- special: `能力触发`(AbilityTriggeredEvent)
- system-request: `攻击请求`(AttackRequestedEvent), `出牌请求`(PlayCardRequestedEvent), `进化请求`(EvolveRequestedEvent), `超进化请求`(SuperEvolveRequestedEvent), `Act请求`(ActRequestedEvent), `换牌提交`(MulliganSubmittedEvent), `结束回合请求`(EndTurnRequestedEvent), `玩家选择提交`(PlayerChoiceSubmittedEvent) - 若这些事件有/补齐元数据

## Effect Category Mapping (Current Metadata)
- base-action: `攻击`(AttackEffectParams), `造成伤害`(DamageEffectParams), `治疗`(HealEffectParams), `进化`(EvolveEffectParams), `出牌`(PlayCardEffectParams), `融合`(FusionEffectParams)
- position-change: `召唤`(SummonEffectParams), `添加卡牌到手牌`(GetTokenEffectParams), `抽牌`(DrawCardEffectParams), `弃牌`(DiscardEffectParams), `返回`(ReturnEffectParams), `消失`(VanishEffectParams), `摧毁`(DestroyEffectParams), `变形`(TransformEffectParams), `公开`(RevealEffectParams)
- temp-state: `添加Buff`(AddBuffEffectParams), `移除Buff`(RemoveBuffEffectParams), `设置关键字`(SetKeywordEffectParams)
- resource-value: `修改PP`(ModifyPPEffectParams), `修改PP上限`(ModifyPPMaxEffectParams), `修改EP`(ModifyEPEffectParams), `修改属性`(ModifyStatsEffectParams)
- turn-flow: `开始回合`(TurnStartEffectParams), `结束回合`(TurnEndEffectParams), `换牌`(MulliganEffectParams)

## UI Behavior
- 事件/效果选择器按 CategoryLabel 分组；默认显示 Visible 类别。
- System/Hidden 事件默认不展示，提供“显示系统事件”开关。
- 未指定分类的元数据归入 `未分类` 组，避免丢失可用项。

## Risks / Trade-offs
- 需要给已有事件/效果补齐分类，工作量较大但一次性。
- ActionRequests 中部分事件未暴露元数据，若需要统一隐藏/展示可考虑补齐标注。

## Migration Plan
1. 后端元数据字段扩展并向前兼容（缺省分类归入未分类）。
2. 前端分组与过滤逻辑上线，默认隐藏 system-request。
3. 分步为事件/效果补齐分类；关键 ActionRequests 先行处理。

## Open Questions
- System 事件默认是否完全隐藏（不可见），还是放在独立分组并可展开？
- 是否需要为前端提供固定排序（CategorySortOrder）以稳定 UI 展示顺序？
