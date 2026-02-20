# Change: Add LeaveField pre-routing while preserving legacy leave-event commit semantics

## Why
当前离场判定分散在多个入口与事件内部，存在以下问题：
- 离场来源与免疫判定不集中，规则一致性难以保证。
- `Destroy/Return/Tuck/Vanish` 既承载“离场决策”又承载“状态提交”，难以在不改大量现有能力的前提下插入统一前置判定。
- 现有“离场转消失”与“无法被能力消失”冲突场景需要一个统一决策层先完成来源与免疫评估，再分流到具体离场事件。

## What Changes
- 新增可提交事件 `LeaveFieldEvent`（系统事件），作为统一离场前置决策入口。
- 现有直接发起 `Destroy/Vanish/Return/Tuck` 的入口，改为先发 `LeaveFieldEvent`。
- `LeaveFieldEvent.Commit` 根据目标、离场意图、来源（`Ability/System`）和关键字免疫进行分流，生成一个具体离场事件：
  - `DestroyEvent` / `CardVanishedEvent` / `ReturnEvent` / `TuckEvent` / 或无后续事件（被免疫拦截）。
- 保持 `Destroy/Vanish/Return/Tuck` 现有语义不变：继续由其 `Commit` 负责生成 `CardMovedEvent`（即不做“信号化”改造）。
- 将离场免疫判定责任迁移到 `LeaveFieldEvent`，并从分散位置收敛（保留必要防御性兜底，避免外部直接注入路径出现破坏性回归）。

### Direct constructor call sites to migrate to LeaveField
以下位置当前直接构造离场事件，需改为构造 `LeaveFieldEvent`：

1. `DestroyEvent` 直接构造点
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
- `Server/Infrastructure/Services/RawEventGameEventRegistry.cs:455`（外部 RawEvent 注入入口，需评估兼容策略）

2. `CardVanishedEvent` 直接构造点
- `Server/Domain/Abilities/Effects/Handlers/VanishEffectHandler.cs:64`
- `Server/Domain/Entities/PlayerState.cs:1146`
- `Server/Domain/Events/FusionEvent.cs:90`（此处为从手牌消失，不应计入改造范围）
- `Server/Domain/Events/CardMovedEvent.cs:192`

3. `ReturnEvent` 直接构造点
- `Server/Domain/Abilities/Effects/Handlers/ReturnEffectHandler.cs:58`
- `Server/Domain/Entities/PlayerState.cs:1117`

4. `TuckEvent` 直接构造点
- `Server/Domain/Abilities/Effects/Handlers/TuckEffectHandler.cs:50`

### Internal behavior to refactor (immunity checks transfer)
1. 破坏免疫判定需转移至 `LeaveFieldEvent`
- `Server/Domain/Events/DestroyEvent.cs:57` (`CanBeDestroyed`)
- `Server/Domain/Abilities/Effects/Handlers/DestroyEffectHandler.cs:66`（发事件前预判）
- `Server/Domain/Events/CombatDestroyResolutionEvent.cs:87`
- `Server/Domain/Events/CombatDestroyResolutionEvent.cs:116`

2. 消失免疫判定需转移至 `LeaveFieldEvent`
- `Server/Domain/Abilities/Effects/Handlers/VanishEffectHandler.cs:56` (`VanishResist`)

3. `Destroy/Vanish/Return/Tuck` 保持提交语义
- `DestroyEvent.Commit` 继续负责产出墓地移动流程。
- `CardVanishedEvent.Commit` 继续负责产出 `CardMoved(... -> Removed)`。
- `ReturnEvent.Commit` 继续负责产出 `AddCardToHandEvent`。
- `TuckEvent.Commit` 继续负责产出 `AddCardToDeckEvent`。

## Impact
- Affected specs: `leave-field-routing`
- Affected code:
  - `Server/Domain/Events/*`（新增 `LeaveFieldEvent`、补充 `EventType`、更新路由与构造点）
  - `Server/Domain/Abilities/Effects/Handlers/*`（改为发 `LeaveFieldEvent`）
  - `Server/Domain/Entities/PlayerState.cs`（离场入口改造）
  - `Server/Infrastructure/Services/RawEventGameEventRegistry.cs`（RawEvent 入口兼容策略）
  - `Server/Domain/Events/EventProcessor.cs`（如需 LeaveField 可见性/忽略策略）
  - 相关测试与场景数据
- Estimated effort:
  - 代码改造 + 回归：约 3.5-7 人天
  - 若包含完整数据迁移与历史能力兼容策略：约 5-9 人天
