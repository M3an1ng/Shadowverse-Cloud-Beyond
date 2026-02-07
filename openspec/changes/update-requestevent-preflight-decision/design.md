## Context
当前事务流程分为分析与提交两段，但决策事件与 RequestEvent 的状态变更边界仍不清晰。部分 RequestEvent/实体方法在提交链早期存在直接状态写入，导致“可撤回阶段”与“已变更状态”并存，且拦截改写无法稳定影响预选择展示。

## Current Flow
1) `GameService.ExecutePlayerActionInitialStepAsync` 产出 `*RequestedEvent` 并进入事务。
2) `GameActionTransactionService.AnalyzePrimaryAbilitiesAsync` 当前仅扫描 `initialEvents` 中实现 `IPlayerActionEvent` 的事件。
3) `CommitTransactionAsync` 内部执行 `ProcessEventRecursivelyAsync` 与 `ProcessEffectAsync`，Choose/Random 常在提交阶段完成决策。
4) 部分 Request 路径在提交链早期存在直接状态写入（如攻击次数、进化权）。

## Target Flow
1) Request 进入事务后先执行 `ExpandRequestToActionRootsAsync`，得到 `ActionRoot`。
2) 对 `ActionRoot` 执行 `PreflightPrimaryAbilitiesAsync`：
   - 预生成 `ChooseEvent/RandomEvent`
   - 先应用 `ApplyBeforeInterceptsAsync`
   - 再进入玩家决策（允许取消）
3) Commit 阶段复用 Preflight 缓存：
   - 禁止取消
   - 回填 `ChooseEvent/RandomEvent` 输出
   - 同事务执行分支能力
4) 所有不可逆状态写入仅发生在事件 `Commit` 阶段。

## Goals / Non-Goals
- Goals:
  - 在主能力可撤回窗口中完成 Choose/Random 预生成、拦截改写、玩家选择与缓存。
  - 提交阶段复用已确认的决策结果，确保同事务内执行分支能力且不可再取消。
  - 统一 RequestEvent 为“Prepare-first”语义，提交前避免不可逆状态变更。
- Non-Goals:
  - 不引入全局历史回滚引擎。
  - 不重写整个能力触发架构。

## Data Model
- `DecisionExecutionKey`
  - `RequestEventId`
  - `ActionRootEventId`
  - `AbilityRuntimeId`
  - `EffectId`
  - `ExecutionIndex`
- `DecisionInputSignature`
  - `OptionIds`
  - `SelectionCount`
  - `AllowRepeat`
  - `WeightsHash`
- `PreflightDecisionState`
  - `ExecutionKey`
  - `InputSignature`
  - `SelectedIndexes`
  - `SelectedAtUtc`

## Decisions
- Decision: 新增 Preflight 决策通道（针对 RequestEvent 派生的主能力链）。
  - Rationale: 让决策 UI 与可撤回语义对齐。
- Decision: Preflight 也执行 Before 拦截，并作用于待决策事件参数。
  - Rationale: 使 `RewriteEventParamsEffect` 改写直接影响玩家看到的选项与数量。
- Decision: Commit 阶段 Choose/Random 一律不可取消，仅消费缓存结果。
  - Rationale: 避免提交期出现“部分已生效再撤回”的事务不一致。
- Decision: RequestEvent 提交前只做校验/派生事件准备，不做不可逆状态写入。
  - Rationale: 从源头降低撤回冲突。
- Decision: 当 Preflight 与 Commit 的决策输入签名不一致时，事务失败并返回可诊断错误，不自动重选。
  - Rationale: 保证事件可回放一致性与行为可解释性，避免隐式分支漂移。
- Decision: AI/脚本路径使用同一 Preflight 通道，通过策略服务自动提交选择结果（可配置 deterministic/random）。
  - Rationale: 保持人机流程同构，便于测试覆盖与行为一致性验证。

## Risks / Trade-offs
- 需要在事务服务中维护“预处理输入签名 -> 决策缓存”一致性，复杂度上升。
- RequestEvent 改造涉及 Attack/Evolve/SuperEvolve 等路径，回归测试量较大。
- 若 Preflight 与 Commit 输入不一致，必须定义严格失效策略（重新决策或失败）。
- 引入 ActionRoot 预展开会增加事务前置计算开销。

## Migration Plan
1) 增加 Preflight 决策入口，并接入 Choose/Random 预生成与拦截改写。
2) 引入决策缓存复用与输入签名校验，提交阶段禁止取消。
3) 将高风险 RequestEvent 路径改为 Prepare-first 语义（先 Attack/Evolve 系列）。
4) 补齐集成测试并在通过后逐步覆盖其他 RequestEvent。

## Compatibility
- 对无决策动作（如不触发 Choose/Random 的普通动作）保持原有行为。
- 对现有 `ChooseEvent/RandomEvent` 事件类型保持兼容，新增字段仅限内部缓存，不写入对外事件契约。
