# Change: DSL编辑器体验完善 + 收藏 + 反向解析

## Why
当前 DSL 编辑器已具备核心封装与逐层编辑能力，但仍存在与既定交互预期不一致的细节（如可编辑区域覆盖不足、中文显示不完整、类型提示上下文不足）。同时，用户复用表达式的需求逐步增强，需要收藏与反向解析能力来降低编辑成本。

## What Changes
- 完善“可点击任意子表达式并加载到工作区”的编辑体验，并统一点击高亮与编辑路径切换。
- 补齐中文可读化显示，确保上下文对象/属性链/函数/运算符均优先使用 displayName。
- 强化类型检测的上下文来源，并以胶囊 √/× 方式呈现；最终在步骤/保存时拦截错误表达式。
- 新增“用户收藏”能力，提供收藏/搜索/插入能力表达式的完整链路。
- 新增“表达式反向解析”能力，使已有 DSL 可恢复为可编辑 UI AST，失败时降级为 rawText。

## Impact
- 受影响的 specs: dsl-editor
- 受影响的代码: src/components/DslEditor.js, src/components/dsl_editor/*, src/components/AbilityEditor.js, Server/Controllers/*, Server/Infrastructure/Persistence/*, Server/Domain/Abilities/DSL/Parser/*
- 受影响的数据: 新增 DSL 收藏相关表与 API
