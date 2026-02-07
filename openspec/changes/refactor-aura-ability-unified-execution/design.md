## Context
Aura 目前由 AuraManager 独立刷新，Ability 由 AbilityManager 统一触发与执行。两条链路在条件、目标解析、时序与调试上存在分裂，且能力影响能力的规则难以一致生效。

## Goals / Non-Goals
- Goals:
  - Aura 触发/解析流程复用 AbilityManager 的统一入口与管理策略。
  - 目标解析/条件判断/触发匹配统一使用 Ability 逻辑。
  - Aura 执行阶段仅刷新 Buff（不创建 Effect）。
  - AuraManager 仅负责 Buff 生命周期与差量刷新。
- Non-Goals:
  - 改变 Buff 的叠加/移除语义。
  - 允许 Aura 执行非持久性效果（伤害/破坏等）。

## Decisions
- 保留 Ability 作为唯一“入口模型”，Aura 通过 AbilityConfig 标识执行分支（Effect vs Aura）。
- 将 Aura 执行拆为 AuraExecutor（或 AuraManager 的瘦身版），只处理 Buff apply/remove 与 ActiveTargets 差量。
- 触发匹配使用 TriggerConfig 的静态规则；不复用 Trigger 运行时计数器。
- Aura 仍禁止 PlayerChoice 目标；目标解析需使用无交互路径。

## Risks / Trade-offs
- 需要在 ProcessSingleAbilityAsync 中引入分支逻辑，避免破坏现有 Effect 执行顺序。
- Aura 刷新会产生 BuffApplied/Removed 事件，需要保留刷新防护以避免事件递归。

## Migration Plan
- 添加 AbilityConfig 执行分支字段（或依据 AuraConfig 触发），逐步迁移现有 Aura 刷新逻辑。
- AuraManager 移除 target/condition/trigger 解析后，统一由 Ability 管道提供 resolvedTargets。

## Open Questions
- 是否为 Aura 分支引入独立的 ExecutionKind 枚举，还是使用 AuraConfig 是否存在作为判定？
- 是否需要对 Aura 刷新引入独立的 CountLimit 语义（默认不限制）？
