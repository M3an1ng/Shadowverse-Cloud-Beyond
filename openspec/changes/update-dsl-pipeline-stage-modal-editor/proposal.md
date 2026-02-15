# Change: 管线阶段编辑改为子 Modal DSL 编辑器

## Why
- 当前管线阶段编辑采用内嵌轻工作区，会与主工作区争夺视觉焦点，用户容易混淆“主表达式编辑”与“阶段子表达式编辑”。
- 复杂阶段表达式在内嵌区域可视范围有限，导致编辑成本高，且难以扩展更多辅助能力（快捷键、分层提示、上下文说明）。

## What Changes
- 保持现有管线阶段列表样式与按钮布局不变。
- 点击阶段“编辑”按钮时，不再在行内展开编辑面板，而是打开子 Modal（子 DSL 编辑器）。
- 子 Modal 内继续复用现有 DSL 节点化编辑能力（包含 Filter/Sort/Take/Distinct/Select 的类型约束与上下文推导）。
- 子 Modal 提供统一的“应用/取消”动作，支持 `Esc` 关闭与遮罩点击关闭（等价于取消）。

## Impact
- 受影响 specs: dsl-editor
- 受影响代码:
  - `src/components/dsl_editor/ui-pipeline.js`
  - `src/components/dsl_editor/pipeline-editor.js`
  - `src/components/DslEditor.js`
  - `src/styles/DslEditor.css`
  - `tests/dsl_editor_pipeline.test.js`
