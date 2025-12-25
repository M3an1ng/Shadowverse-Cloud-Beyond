# Change: 将 AbilityTag 统一为 EffectKeyword，并补全 CardKeyword（潜行/屏障/威压/灵气）规则

## Why
- 当前后端能力配置中的 `AbilityTag` 是 `string`，但前端枚举源已映射到 `EffectKeyword`（`/api/localization/enums/AbilityTag`），导致：
  - 能力标签缺少类型约束（存储/传输可出现任意字符串，运行期才暴露问题）；
  - 代码层面无法用编译期保障“标签集合与触发标签一致”。
- `CardKeyword` 中存在多项关键字已定义但未在核心战斗/目标选择逻辑中生效（例如 `Ambush/Shield/Pressure/Aura`），会造成：
  - 目标选择与可用性检查不一致（UI 显示可用但进入选择时无合法目标，或反之）；
  - “必须选择目标”的能力在边界情况下行为不正确（例如场上只有 1 个 `Aura` 随从时，本应不可使用的指向性能力仍被允许发起）。

## What Changes
- **AbilityTag 类型统一**
  - 将能力配置与运行时能力的 `AbilityTag` 统一为 `EffectKeyword`（而非自由 `string`）。
  - 保持存储层（DB）的列类型不变（仍为 TEXT/string），通过显式转换保证向后兼容。
  - 为系统能力提供明确标签（建议：`EffectKeyword.System` 或 `EffectKeyword.None` + `AbilityType` 区分），避免现有 `"System"` 字符串成为“逃逸值”。
- **补全 CardKeyword 规则**
  - `Ambush (潜行)`: 防止被对手“指向性选择”（targeted）并在必要时影响攻击合法性。
  - `Shield (屏障/护盾)`: 抵消下一次伤害并移除该关键字。
  - `Aura (灵气)`: 无法成为能力指定的目标（ability targeted），并保证在“必须选择目标”时正确阻断出牌/发动。
  - `Pressure (威压)`: 无法成为攻击指定的目标（attack declared target），并保证在存在 `Ward` 等强制攻击约束时边界行为一致（例如唯一可攻击目标同时为 `Pressure` 时应判定无法攻击）。
  - `Drain (吸血)`: 仅在随从进行攻击并造成战斗伤害时触发回复；由能力/效果造成的伤害 MUST NOT 触发吸血。
  - `Bane (必杀)`: 仅在随从进行攻击并对敌方随从造成战斗伤害时触发“被伤害随从直接被消灭”；由能力/效果造成的伤害 MUST NOT 触发必杀。
  - `Shackle (无法攻击)`: 具有该关键字的随从无法宣告攻击（无论是否有 Rush/Storm/是否进化/是否已过召唤回合）。
- **一致性与测试**
  - 能力目标：目标可用性检查（`CheckTargetAvailabilityAsync`）与实际目标解析（`ResolveTargetsForAbilityAsync`）必须共享同一套关键字过滤规则（覆盖 `Aura/Ambush`）。
  - 攻击目标：攻击合法性检查与前端可选目标列表（若存在）必须共享同一套关键字过滤规则（覆盖 `Pressure`）。
  - 增加后端测试覆盖关键边界：只有 Aura、Pressure、Shield、Ambush，以及 Drain/Bane 的“仅战斗触发”差异、Shackle 的“无法攻击”等。

## Non-Goals
- 不在本变更中一次性补完所有未实现的 `CardKeyword`（例如 `Bane/Drain/Freeze/...`）；本变更聚焦用户提出的 4 项并建立可扩展的“目标可选规则”管线。
- 不在本变更中重写目标查询 DSL（仍沿用现有 `QueryExecutor` 与 `TargetSelectionService` 管线），仅在其结果上附加“规则过滤”。

## Impact
- Affected code (planned):
  - Backend:
    - `Server/Domain/Abilities/AbilityConfig.cs`, `Server/Domain/Abilities/Common/IAbility.cs`, `Server/Domain/Abilities/Ability.cs`
    - `Server/Domain/Entities/AbilityDefinition.cs`, `Server/Infrastructure/Data/ApplicationDbContext.cs`（值转换/映射）
    - `Server/Domain/Abilities/Targets/TargetSelectionService.cs`, `Server/Services/GameService.cs`（Aura/Ambush 可用性一致性）
    - `Server/Domain/Entities/CardInstance.cs`（Pressure 攻击合法性）
    - `Server/Domain/Events/DamageEvent.cs`（Shield 伤害拦截）
  - Tests:
    - `Server.Tests` 新增关键字与目标选择边界用例
