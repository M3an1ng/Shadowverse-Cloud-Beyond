## Context
当前离场流程存在两类耦合问题：
1. 决策耦合：`Destroy/Vanish/Return/Tuck` 同时承担“是否允许离场”和“如何提交状态变更”。
2. 判定分散：免疫判定散落在事件 `Commit` 和 handler 预判中，无法保证来源（`Ability/System`）一致解释。

本变更采用“前置统一决策 + legacy 事件保持提交语义”的折中方案：
- 新增 `LeaveFieldEvent` 统一离场判定与分流。
- `Destroy/Vanish/Return/Tuck` 继续负责各自现有提交语义（保持兼容性与低回归风险）。

## Goals
- 将离场来源判定与免疫判定统一到一个入口（`LeaveFieldEvent`）。
- 维持现有 `Destroy/Vanish/Return/Tuck` 的提交语义与下游触发链。
- 最小化对现有能力 DSL 与事件生态的破坏。

## Non-Goals
- 不将 `Destroy/Vanish/Return/Tuck` 重构为纯信号事件。
- 不在本次中全面迁移所有历史能力配置到 `LeaveField` 触发。
- 不改动 `CardMovedEvent` 的核心状态提交职责。

## Current direct emitters (must migrate)
### Destroy constructors
- `Server/Domain/Abilities/Effects/Handlers/DestroyEffectHandler.cs:73`
- `Server/Domain/Abilities/Effects/Handlers/EarthRiteEffectHandler.cs:71`
- `Server/Domain/Events/CombatDestroyResolutionEvent.cs:81`
- `Server/Domain/Events/CombatDestroyResolutionEvent.cs:89`
- `Server/Domain/Events/CombatDestroyResolutionEvent.cs:110`
- `Server/Domain/Events/CombatDestroyResolutionEvent.cs:118`
- `Server/Domain/Events/CountdownTickEvent.cs:66`
- `Server/Domain/Events/DamageEvent.cs:137`
- `Server/Domain/Events/StatChangedEvent.cs:227`
- `Server/Domain/Events/SummonEvent.cs:58`
- `Server/Domain/Events/SummonEvent.cs:60`
- `Server/Infrastructure/Services/RawEventGameEventRegistry.cs:455`

### Vanish constructors
- `Server/Domain/Abilities/Effects/Handlers/VanishEffectHandler.cs:64`
- `Server/Domain/Entities/PlayerState.cs:1146`
- `Server/Domain/Events/FusionEvent.cs:90`
- `Server/Domain/Events/CardMovedEvent.cs:192`

### Return constructors
- `Server/Domain/Abilities/Effects/Handlers/ReturnEffectHandler.cs:58`
- `Server/Domain/Entities/PlayerState.cs:1117`

### Tuck constructors
- `Server/Domain/Abilities/Effects/Handlers/TuckEffectHandler.cs:50`

## Internal checks to transfer into LeaveField
### Destroy immunity checks
- `Server/Domain/Events/DestroyEvent.cs:57` (`CanBeDestroyed`)
- `Server/Domain/Abilities/Effects/Handlers/DestroyEffectHandler.cs:66`
- `Server/Domain/Events/CombatDestroyResolutionEvent.cs:87`
- `Server/Domain/Events/CombatDestroyResolutionEvent.cs:116`

### Vanish immunity checks
- `Server/Domain/Abilities/Effects/Handlers/VanishEffectHandler.cs:56` (`VanishResist`)

## Proposed event contract
`LeaveFieldEvent` 输入参数建议：
- `TargetCard`（必须）
- `LeaveKind`（`Destroy/Vanish/Return/Tuck`）
- `ReasonSource`（`Ability/System`）
- `SourceCard`（可空）
- `OriginalZone`（默认取当前区）

输出行为：
- 根据统一规则生成 0 或 1 个 legacy 离场事件。
- 由 legacy 事件继续提交状态变化并派生 `CardMovedEvent`。

## Routing rules
1. 公共前置：
- 仅处理离场相关目标（默认 `Field`，可扩展）。
- 目标不存在或不在可处理区域时返回 `Success + NoOp`。

2. 免疫判定统一入口：
- `LeaveKind=Destroy` 且 `ReasonSource=Ability` 时，调用统一破坏免疫规则（等价于当前 `CanBeDestroyed`）。
- `LeaveKind=Vanish` 且 `ReasonSource=Ability` 且目标在 `Field` 且具 `VanishResist`：
  - 路由降级为 `Destroy`（满足“无法被能力消失时按正常破坏处理”）。

3. 分流结果：
- `Destroy` -> `DestroyEvent`
- `Vanish` -> `CardVanishedEvent`
- `Return` -> `ReturnEvent`
- `Tuck` -> `TuckEvent`
- `NoOp` -> 不生成后续事件

## Legacy event behavior (explicitly preserved)
- `DestroyEvent.Commit` 继续产出墓地移动链路（当前通过 `PlayerState.DestroyCard`）。
- `CardVanishedEvent.Commit` 继续产出 `CardMoved(... -> Removed)`。
- `ReturnEvent.Commit` 继续产出 `AddCardToHandEvent`（再进入 `CardMoved`）。
- `TuckEvent.Commit` 继续产出 `AddCardToDeckEvent`（再进入 `CardMoved`）。

## Compatibility strategy
- 事务顺序保持现状：`Before Intercept -> Commit`。
- 保持 legacy 事件类型存在，避免现有触发器生态立即失效。
- `RawEventGameEventRegistry` 的 `destroy` 外部注入路径应明确：
  - 方案 A：映射到 `LeaveFieldEvent(LeaveKind=Destroy)`（推荐）
  - 方案 B：保留直接 `DestroyEvent` 但仅作为调试后门，并记录告警。

## Risks / Trade-offs
- 同时存在 `LeaveField` 与 legacy 离场事件，会在过渡期增加事件层级。
- 若 legacy 的 Before-Replace（`Destroy/Return/Tuck -> Vanish`）不做治理，可能与 `LeaveField` 规则叠加。
- 需要额外回归以确认墓地计数、离场触发、日志与回放顺序未变化。

## Test plan
- 单元测试：
  - `LeaveField` 对 `DestroyResist`、`VanishResist`、`Ability/System` 的路由矩阵。
- 集成测试：
  - `LeaveField -> legacy -> CardMoved` 全链路行为。
  - 冲突场景（“无法被能力消失” + “离场转消失”）回归。
- 回放/DTO 测试：
  - 确认不引入重复离场广播或顺序倒置。
