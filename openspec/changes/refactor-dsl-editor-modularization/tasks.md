## 1. Implementation
- [x] 1.1 盘点 DslEditor 职责分区与依赖（渲染、管线、解析、类型校验、收藏/API、状态/路径）。
- [x] 1.2 抽离基础 UI 渲染：节点渲染与胶囊/占位样式到 `src/components/dsl_editor/ui-nodes.js`。
- [x] 1.3 抽离 UI 交互菜单：选项菜单与子菜单创建逻辑到 `src/components/dsl_editor/ui-menus.js`。
- [x] 1.4 抽离管线编辑：状态与编辑逻辑到 `src/components/dsl_editor/pipeline-editor.js`。
- [x] 1.5 抽离管线 UI：阶段列表、编辑面板渲染到 `src/components/dsl_editor/ui-pipeline.js`。
- [x] 1.6 抽离解析与类型校验：`buildAstFromExpression`/`calculateASTReturnType`/`validateExpressionSemantics` 到 `src/components/dsl_editor/ast-parse.js`、`src/components/dsl_editor/type-check.js`。
- [x] 1.7 抽离收藏与解析 API：收藏查询与解析服务请求到 `src/components/dsl_editor/services/`。
- [x] 1.8 收敛编辑路径与状态管理：路径栈、选中态与上下文覆盖整理到 `src/components/dsl_editor/editing-session.js`。
- [x] 1.9 全量更新导入/导出与调用点，避免循环依赖并保持行为一致。
- [x] 1.10 补充最小验证清单（主表达式、工作区、管线编辑、收藏、解析回退）。

## 2. Further Modularization (DslEditor 继续瘦身)
- [x] 2.1 抽离可点击主表达式与节点递归渲染到 `src/components/dsl_editor/ui-expression.js`。
- [x] 2.2 抽离收藏 UI 与工作区头部到 `src/components/dsl_editor/ui-favorites.js`、`src/components/dsl_editor/ui-workspace.js`。
- [x] 2.3 抽离可读标签与预览文本到 `src/components/dsl_editor/ui-labels.js`、`src/components/dsl_editor/ui-preview.js`。
- [x] 2.4 抽离胶囊样式与属性链展示辅助到 `src/components/dsl_editor/ui-nodes.js`（`getEncapsulationStyle`/`createStatusBadge`/`removeBrackets`/`getStyleForValueType`/`getReadablePropertyChain*`）。
- [x] 2.5 抽离 AST DTO 映射与链路构建到 `src/components/dsl_editor/ast-mapping.js`（`mapDslAstDtoToUiAst`、`mapLiteralDtoToNode`、`mapIdentifierDtoToNode`、`mapBinaryOperationDtoToNode`、`mapUnaryOperationDtoToNode`、`mapFunctionCallDtoToNode`、`mapListOperationFromMethodCall`、`flattenMemberAccessPath`、`createPropertyChainNode`）。
- [x] 2.6 抽离选项生成与筛选策略到 `src/components/dsl_editor/options-provider.js`（`getCurrentEditingOptions`/`getOptionsForIncompleteNode`/`getOptionsForCompleteNode`/`getOptionsForType`/`getBooleanExpressionOptions`/`getDataSourceOptions`/`getContextObjectsForOptions`/`getTriggeringEventFieldOptions`）。
- [x] 2.7 抽离 AST 编辑/工作区流程到 `src/components/dsl_editor/ast-editing.js`（`createRootNode`/`extendAST`/`handleEnterEditModeForSlot`/`saveCurrentLayer`/`performASTGoBack`/`create*FromCurrent`/`add*Operand*`）。
- [x] 2.8 抽离 UI 反馈与状态提示到 `src/components/dsl_editor/ui-feedback.js`（`showExpressionError`/`showError`/`setParseStatus`/`getParseStatusInfo`）。
- [x] 2.9 收敛 DslEditor 为 orchestrator：仅保留状态、组合与薄封装；清理冗余 import/委托链。
- [x] 2.10 更新最小验证清单覆盖新增拆分点（映射/选项/编辑流/提示反馈）。

## 3. UI Styling Refactor (JS -> CSS)
- [x] 3.1 盘点各 UI 模块中的内联样式与动态样式（`ui-expression.js`/`ui-nodes.js`/`ui-pipeline.js`/`ui-favorites.js`）。
- [x] 3.2 定义 DSL 编辑器样式令牌与基础 class（CSS 变量 + 节点/胶囊/状态 class）。
- [x] 3.3 将可静态化的内联样式迁移到 CSS，仅保留必要的动态样式。
- [x] 3.4 通过 class/data-attribute 表达节点类型与状态（封装、空槽位、选中、高亮）。
- [x] 3.5 补充样式回归清单（主表达式、工作区、胶囊、管线、收藏、错误提示）。
