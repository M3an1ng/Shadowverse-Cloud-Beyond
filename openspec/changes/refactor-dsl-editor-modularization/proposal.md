# Change: DSL 编辑器模块化重构

## Why
- 现有 DslEditor 单文件规模过大（6000+ 行），职责耦合度高，影响维护与迭代速度。
- 计划的管线编辑与占位提示优化需要更清晰的模块边界作为前置条件。

## What Changes
- 将渲染逻辑、管线编辑逻辑、解析/校验逻辑拆分为独立模块。
- 收敛状态管理与编辑路径逻辑，减少跨模块隐式依赖。
- 维持现有功能与交互行为不变，仅调整代码结构。

## Impact
- 受影响 specs: dsl-editor
- 受影响代码: src/components/DslEditor.js, src/components/dsl_editor/*
- 依赖关系: 本变更作为 update-dsl-editor-pipeline-editing 的前置条件