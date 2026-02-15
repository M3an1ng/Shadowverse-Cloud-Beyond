# Change: 强化管线阶段子编辑器并保持 self 上下文始终可访问

## Why
- 现有管线阶段子编辑器基于轻工作区，复杂表达式构建能力不足。
- 在部分重置/重渲染路径中，`self` 上下文会丢失，影响筛选表达式连续编辑。

## What Changes
- 管线阶段子编辑器改为完整 DSL 编辑体验（保留子 Modal 入口），支持主编辑器级别的工作区与完整表达式显示。
- 引入固定上下文机制：在管线阶段子编辑器中，`self` 像 `context` 一样始终可访问。
- 保持表达式初始化/清空语义不变（仍为空占位），不再强制将 `self` 写入表达式根。
- 保持阶段提交/取消语义不变，继续复用现有 AST 提交流程。

## Impact
- 受影响 specs: dsl-editor
- 受影响代码:
  - `src/components/DslEditor.js`
  - `src/components/dsl_editor/pipeline-editor.js`
  - `src/components/dsl_editor/ast-editing.js`
  - `src/components/dsl_editor/ui-workspace.js`
  - `tests/dsl_editor_pipeline.test.js`
