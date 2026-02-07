## Context
- 当前流程：Resolve 决策 -> 生成 Choose/Random 事件 -> 处理事件 -> 执行分支能力。
- 现状导致 RewriteEventParamsEffect 改写事件参数不影响决策。

## Goals / Non-Goals
- Goals:
  - 让拦截后的事件参数影响最终决策。
  - 保持回放一致性与事件输出可追溯。
- Non-Goals:
  - 不改变事件类型数量（方案 B：单事件）。
  - 不引入新的外部依赖。

## Decisions
- Decision: 将 Choose/Random 的决策生成延后到事件处理阶段（Commit 或等价位置）。
- Decision: 分支能力执行改为在事件输出生成后触发。

## Alternatives considered
- 方案 A（Request/Result 双事件）：语义清晰但新增事件类型与流程复杂度。
- 方案 B（单事件延后决策）：改动集中且不扩展事件类型，选用。

## Risks / Trade-offs
- 风险：决策结果依赖事件输入，需确保事件输入完整可用。
- 权衡：事件处理阶段需要访问决策所需上下文（选项、权重等），需要调整数据流。

## Migration Plan
1. 事件先生成，允许拦截改写输入。
2. 在事件处理阶段生成选择结果并落入输出参数。
3. 分支能力执行改为依赖事件输出。
4. 更新测试覆盖拦截场景。

## Open Questions
- 事件 Commit 是否应直接触发分支能力，还是由事务服务统一调度？
