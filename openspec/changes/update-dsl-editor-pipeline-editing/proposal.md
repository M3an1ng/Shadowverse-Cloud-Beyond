# Change: 更新 DSL 编辑器管线编辑交互与占位提示

## Why
- 近期管线编辑交互出现偏差，未完成阶段会被写入表达式，且编辑方式偏离“节点化选择”预期。
- 空白表达式/占位符的提示不统一，影响玩家对类型与编辑状态的理解。

## What Changes
- 管线阶段编辑统一使用 DSL 节点编辑逻辑，避免直接文本输入。
- Filter/Sort 阶段使用 self 上下文进行引导；Select 阶段限定策略为 random/first/last。
- 未完成的管线阶段不写入主表达式/工作区表达式。
- 空白表达式与占位符统一显示中文类型提示，并使用提醒性样式。

## Impact
- 受影响 specs: dsl-editor
- 受影响代码: src/components/DslEditor.js, src/components/dsl_editor/core.js, src/components/dsl_editor/ui-workspace.js, src/styles/DslEditor.css
