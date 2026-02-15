## Context
- 管线阶段编辑已迁移到子 Modal，但内部仍使用轻工作区编辑模式。
- 轻工作区模式在复杂表达式构建、上下文稳定性（`self`）方面存在体验与可靠性问题。

## Goals / Non-Goals
- Goals:
  - 让管线阶段子编辑器获得与主 DSL 编辑器一致的表达式构建能力。
  - 保证 `self` 在目标阶段始终可访问，且不依赖临时 `contextOverride` 状态。
- Non-Goals:
  - 不修改管线阶段 AST 结构与序列化协议。
  - 不新增 DSL 语法。

## Decisions
- Decision: 子编辑器启用完整编辑模式（非轻工作区限制），但仍在管线 Modal 内渲染。
  - Rationale: 直接复用主编辑器能力，最小化复杂表达式场景的功能缺失。

- Decision: 引入 `fixedRootContext` 作为持久上下文来源，将 `self` 注入上下文候选和解析链路。
  - Rationale: 保证 `self` 像游戏上下文对象一样始终可访问，而不是作为强制表达式起点。

- Decision: 不改动表达式初始化/清空行为（仍为空占位），仅增强上下文可访问性。
  - Rationale: 避免把“可访问上下文”误实现成“默认表达式内容”。

## Risks / Trade-offs
- Risk: 子编辑器 UI 信息量增加。
  - Mitigation: 关闭收藏工具栏，保留核心表达式与工作区。
- Risk: 固定根上下文在非目标阶段误触发。
  - Mitigation: 固定上下文仅影响候选与解析，不直接写入表达式 AST。
