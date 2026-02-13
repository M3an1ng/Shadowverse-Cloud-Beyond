## Context
当前事务管线已具备 RequestEvent 预展开与部分决策预检能力，但“目标预检、决策预检、分支能力预检”仍分散在不同阶段。业务要求升级为统一策略：提交前尽可能完成所有可确定步骤，并支持撤销；提交后不应再出现交互。

## Goals / Non-Goals
- Goals:
  - 统一预检编排入口，消除“同类步骤在不同阶段重复解析”的结构性问题。
  - 支持 `Choose/Random` 分支能力的递归预检（含分支目标选择，可撤销）。
  - 提交阶段仅消费预检结果，不发起新的玩家交互。
  - 为预检与提交建立可验证一致性（签名/指纹）。
- Non-Goals:
  - 不重写能力 DSL。
  - 不一次性重构所有事件类型的业务语义，仅先覆盖玩家动作主链路。

## Architecture

### 1. Core Objects
- `PreflightSession`
  - `TransactionId`
  - `ActionRootEventIds`
  - `TargetChoiceSnapshots`
  - `DecisionChoiceSnapshots`
  - `CommitPlan`
  - `StateFingerprint`

- `AbilityPreflightNode`
  - `AbilityRuntimeId`
  - `TriggerEventId`
  - `PhaseState`（ConditionResolved / TargetsResolved / DecisionResolved / BranchExpanded / EffectsMaterialized）
  - `ResolvedTargets`
  - `DecisionEvents`
  - `BranchAbilityRuntimeIds`
  - `MaterializedEffects`

- `CommitPlan`
  - `PlanItems`（按执行顺序）
  - `DecisionSignatures`
  - `TargetSignatures`
  - `StateFingerprint`

### 2. Unified Pipeline
对每个动作根事件，按同一管线处理主能力与分支能力：
1. ResolveTriggerAndConditions
2. ResolveNonChoiceTargets
3. ResolveTargetChoices (cancellable)
4. ResolveDecision (Choose/Random, cancellable)
5. ExpandDecisionBranches
6. ResolveBranchTargetChoices (cancellable)
7. MaterializeEffects

### 3. Commit Contract
- Commit 前校验 `StateFingerprint` 与签名一致性。
- Commit 阶段禁止新交互：
  - 目标选择与决策选择均必须命中 `PreflightSession` 快照。
  - 若未命中则失败（`PreflightIncomplete`）。
- Commit 仅执行 `CommitPlan` 内的效果与事件，不再进行新的“可取消”路径。

## Key Decisions
- Decision: 默认所有能力尽可能走完整预检步骤。
  - Rationale: 满足“提交前可撤销”的统一业务语义。
- Decision: `Choose` 分支能力递归纳入预检并缓存分支目标选择结果。
  - Rationale: 解决当前分支目标选择在提交期出现、不可撤销的问题。
- Decision: 提交阶段禁止交互请求，改为“命中快照 or 失败”。
  - Rationale: 保证事务边界清晰，避免半提交状态。
- Decision: 保留 Before/After 拦截语义，但将其纳入预检编排上下文中统一执行。
  - Rationale: 保持现有能力效果行为一致。

## Integration Points
- `GameActionTransactionService`
  - 新增：
    - `BuildPreflightSessionAsync`
    - `PreflightAbilityGraphAsync`
    - `BuildCommitPlanAsync`
    - `ExecuteCommitPlanAsync`
  - 现有 `AnalyzePrimaryAbilitiesAsync` 逐步迁移为编排入口适配层。

- `AbilityManager`
  - `ProcessSingleAbilityAsync` 增加执行模式（`Preflight` / `CommitConsumePlan`）。
  - 在 `CommitConsumePlan` 模式下，不允许产生新的交互请求。

- `TargetSelectionService`
  - 增加读取预检快照的解析分支。
  - Commit 模式中，若无快照则返回失败，不发起 `WaitForPlayerChoiceAsync`。

## Risks / Trade-offs
- 编排状态对象增多，事务服务复杂度上升。
- 对现有缓存键稳定性要求更高（RuntimeId/EffectIndex/ExecutionIndex 必须稳定）。
- 需要补齐回归测试以覆盖递归分支与多轮 Repeat 场景。

## Migration Plan
1. 第一步：引入 `PreflightSession`/`CommitPlan` 数据结构，不改变既有行为。
2. 第二步：接入 `Choose` 分支能力递归预检与分支目标可撤销选择。
3. 第三步：提交阶段启用“禁止新交互”硬约束。
4. 第四步：移除旧的分散预检分支与重复解析逻辑。

## Validation Strategy
- 单元测试：签名匹配、快照命中、缺失快照失败。
- 集成测试：主能力 + choose 分支 + 分支目标选择的可取消流程。
- 回归测试：无目标选择能力、无 choose 能力、拦截改写能力。
