## Why

当前 `tribe` 在系统中仍然是裸字符串列表，所有判断都依赖直接字符串比较，缺少统一的规范化、特殊语义解释与 DSL 兼容层。这使得多 `tribe` 卡牌去重、特殊 `tribe`（如“具有所有已知 tribe”）以及基于离场历史的卡牌选择规则无法稳定实现，并且会继续放大历史数据、编辑器输入、运行时 Buff 与 DSL 表达式之间的不一致风险。

口袋银河的需求把这个问题暴露得很直接：系统需要从“本场对战中自己的离场从者”中，按名称与 `tribe` 组合规则选择候选；当前 `List<string>` 形态既不能直接作为稳定去重键，也缺少对“特殊 tribe 覆盖所有普通 tribe”的统一定义。现在需要把 `tribe` 从“字符串约定”升级为“数据化配置 + 统一语义解释”。

## What Changes

- 新增基于数据库的 `tribe` 定义与注册能力，用于配置规范名、别名、是否保留、是否为特殊语义 `tribe` 及其行为。
- 新增运行时 `tribe` 语义层，统一处理规范化、去重、包含判断、交集判断与“特殊 tribe 覆盖所有已知普通 tribe”的展开规则。
- 保持卡牌定义、运行时卡牌实例与编辑器对外仍使用字符串 `tribe` 列表，不引入写死枚举或 DSL 语法破坏性变更。
- 更新 DSL 的 `self.Tribe == "X"` 与 `contains(self.Tribe, "X")` 判定逻辑，使其走统一的 `tribe` 语义层，而不是继续使用裸字符串比较。
- 新增基于事件历史快照的离场从者 `tribe` 候选展开与去重规则，支持多 `tribe` 卡牌与特殊 `tribe` 在口袋银河等效果中的稳定选择。
- 为卡牌保存、种子导入与 AddTribe 效果等入口增加 `tribe` 规范化与自动注册流程，避免运行时出现同语义不同字符串的脏数据。

## Capabilities

### New Capabilities
- `tribe-registry-and-semantics`: 通过数据库配置与运行时语义层统一管理普通 `tribe`、保留 `tribe`、别名与特殊行为。
- `dsl-tribe-semantics-compatibility`: 在不修改现有 DSL 表达式写法的前提下，使 `tribe` 判断遵循统一语义层。
- `history-tribe-distinct-selection`: 基于离场历史快照提供可展开、多 `tribe` 感知的 `tribe` 候选选择规则，支撑口袋银河等效果。

### Modified Capabilities
- None.

## Impact

- Affected code:
  - `Server/Controllers/CardsController.cs`
  - `Server/Infrastructure/Data/*`
  - `Server/Services/GameDataProvider.cs`
  - `Server/Domain/Entities/CardDefinition.cs`
  - `Server/Domain/Entities/CardInstance.cs`
  - `Server/Domain/Events/*`
  - `Server/Domain/Abilities/DSL/Evaluation/*`
  - `Server/Domain/Abilities/DSL/Functions/*`
  - `Server/Domain/Abilities/Effects/Handlers/AddTribeEffectHandler.cs`
  - `Server/Domain/Buffs/*`
  - `src/cardEditor.js`
- Affected APIs:
  - card save/update flows
  - metadata/editor tribe suggestion or validation endpoints if introduced
- Affected systems:
  - card data seeding
  - game data cache
  - DSL evaluation
  - effect resolution based on event history
