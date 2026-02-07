## Context
需要引入 removeAbilityEffect，作为 GrantAbility 的反向能力，用于移除卡牌实例上的能力。移除需要支持持续时间，且允许按 Ability.DefinitionId / AbilityKeyword / ALL 三种模式控制范围。ALL 模式要求包含 aura。

## Goals / Non-Goals
- Goals:
  - 提供 RemoveAbilityEffect，支持 RemoveMode=ID/Keyword/ALL。
  - 能力移除采用“抑制”而非销毁：到期后能力自动恢复。
  - ALL 模式必须清除被抑制能力产生的 aura。
  - 触发链路、Act/Fusion 可用性、UI 能力摘要均使用“有效能力列表”。
- Non-Goals:
  - 不引入按运行时 Ability.RuntimeId 精确移除模式。
  - 不改动现有 GrantAbility 的语义。

## Decisions
- Decision: 新增 AbilitySuppressionBuffEffect（或同义命名）作为 Buff 效果类型，承载移除规则。
  - Reason: 与现有 Buff 生命周期一致，支持 DurationType 自动恢复。
- Decision: RemoveMode=ID/Keyword/ALL。
  - ID 模式使用 Ability.DefinitionId 匹配；未设置 DefinitionId 的能力不参与 ID 匹配。
- Decision: 能力抑制不修改 CardInstance.Abilities 列表，只在 AbilityManager 中做过滤。
  - 提供 AbilityManager.GetEffectiveAbilitiesForCard(cardId, context) 与 IsAbilitySuppressed(ability, context)。
- Decision: Aura 处理
  - 在抑制生效时，对被抑制能力的 runtimeId 调用 AuraManager.UnregisterBySourceAbilities 以清理 aura。
  - 在抑制解除时，主动对相关能力执行 aura 刷新。

## Risks / Trade-offs
- 需要在多个能力访问点统一使用“有效能力列表”，否则会出现 UI/触发/请求不一致。
- Aura 恢复需要明确触发时机；若仅等待后续刷新，可能导致恢复延迟。

## Migration Plan
- 新增效果与 Buff 类型不影响现有卡牌数据。
- 现有卡牌无需迁移；仅在使用新效果时生效。

## Open Questions
- None.
