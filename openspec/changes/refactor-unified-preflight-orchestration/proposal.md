# Change: Refactor Unified Preflight Orchestration

## Why
当前预检能力分散在多个路径中（主能力目标预检、Choose/Random 预检、提交阶段补解析），导致以下问题：
1. 预检职责边界不清晰，新增规则容易引入阶段错配。
2. `Choose` 分支中“需要目标选择”的能力默认在提交阶段才请求目标，无法在提交前撤销。
3. 提交阶段仍可能出现新的交互请求，破坏“提交前可撤销，提交后不可交互”的事务语义。

业务目标是：默认所有能力尽可能在预检阶段按步骤推进，直到最终提交前都可撤销；提交阶段只消费预检结果，不再产生新的玩家交互。

## What Changes
- 引入统一预检编排模型：`PreflightSession + CommitPlan`。
- 将能力执行统一为可编排步骤：条件、目标、决策、分支展开、分支目标、效果物化。
- 默认策略改为“尽可能预检”：所有可在预检确定的信息都在预检完成，并支持取消。
- `Choose/Random` 的分支能力纳入预检，分支中的目标选择同样在预检阶段完成且可取消。
- 提交阶段禁止新增玩家交互请求；若出现未完成选择则事务失败并返回诊断信息。
- 增加预检/提交一致性校验（输入签名与状态指纹），不一致时拒绝提交。

## Impact
- Affected specs: `event-decision`
- Affected code:
  - `Server/Services/GameActionTransactionService.cs`
  - `Server/Domain/Abilities/AbilityManager.cs`
  - `Server/Domain/Abilities/Targets/TargetSelectionService.cs`
  - `Server/Services/PendingActionService.cs`
  - `Server/Domain/Abilities/Ability.cs`

## Out Of Scope
- 不改动 DSL 语法与前端编辑器配置结构。
- 不引入全局历史状态快照回滚引擎。
- 不调整现有事件存储介质与回放文件格式版本。
