## Context
当前持久性光环仍依赖 Effect 路径注册。为了让持久性能力“总是通过 Buff 增删与管理实现”，需要将 Aura 作为 AbilityConfig 的一级配置，并由 AuraManager 直接产出/移除 Buff 实例。

## Goals / Non-Goals
- Goals:
  - Ability 继续保留 Targets/Conditions/Properties，作为 Aura 默认输入。
  - AuraConfig 产出 Buff：刷新时通过 Buff add/remove 管理效果。
  - AuraConfig 可覆盖 Ability 级 Targets/Conditions/Properties。
  - Conditions 作为 Aura 的整体开启/关闭开关。
- Non-Goals:
  - 改变 Buff 基本语义或叠加规则。
  - 允许 Aura 使用非幂等/一次性效果（伤害/破坏）。

## Decisions
- AuraConfig 定义 refresh triggers + BuffTemplate（不使用 Effect/EffectConfig）。
- AuraConfig 默认继承 Ability 的 Targets/Conditions/Properties。
- Conditions 仅用于整体开关；目标过滤优先用 TargetQuery 完成。
- BuffTemplate 仅允许安全的持续性效果（stat/keyword/grant ability）。
- AuraManager 仍是唯一的刷新与 Buff 生成入口。

## Risks / Trade-offs
- 需要确保 Ability-level Targets/Conditions 与 Aura overrides 的优先级一致且可预测。
- 可能需要调整 DSL/JSON loader 以支持 AuraConfig 而不破坏旧格式。

## Migration Plan
- 不保留旧 AddAura effect 路径；直接以 AuraConfig 为唯一配置方式。

## Open Questions
- 是否需要允许多个 AuraConfig 共享同一 Targets/Conditions（默认继承即可）。
- 是否需要为 AuraConfig 提供独立的目标条件（per-target gating），还是仅依赖 TargetQuery。
