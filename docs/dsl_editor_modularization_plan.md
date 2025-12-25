# DSL 编辑器模块化拆分计划（Phase B 准备）

本计划在不影响现有功能的前提下，对 src/components/DslEditor.js 进行模块化拆分，降低单文件复杂度，提升可维护性与可测试性。拆分将以“小步迁移、随迁随测”为原则，先创建模块骨架，再逐步迁移实现。

## 目标
- 按职责划分：数据/AST、路径与保存、UI 渲染（主表达式/工作区）、操作符与选项策略
- 统一对外接口，确保现有调用方（DslEditor 类）平滑过渡
- 为 Phase B 的 UI 重构（统一封装 capsule/编辑高亮）提供清晰边界

## 拆分结构（建议）
- src/components/dsl_editor/
  - core.js：
    - ASTNode/ValueNode/OperatorNode 定义与 clone、toExpression、validate 等
    - createValueNode/createOperatorNode、inferOperatorReturnType/isLogicalOperator/isComparisonOperator
    - validateAST、findIncompleteNode/findRightmostLeafNode
  - path.js：
    - 编辑路径栈 push/pop/peek/clear 的纯函数实现
    - writeBackToParentSlot（仅处理 left/right；self 结合上层路径）
  - operators.js：
    - getSmartOperators/getTextOperators/getEnumOperators
    - getOptionsForType/集合函数与 listQuery 过滤
  - ui-main.js：
    - 主表达式渲染（封装 capsule、当前编辑高亮、可点击进入编辑）
  - ui-workspace.js：
    - 工作区渲染（按钮、预览、面包屑、添加节点弹出等）

注意：初期仅创建模块骨架与导出接口，具体实现分阶段迁移，确保每次变更可回退。

## 接口约定（初稿）
- core.js 导出：
  - class ASTNode/ValueNode/OperatorNode
  - function createValueNode(value, returnType, metadata)
  - function createOperatorNode(operator, returnType)
  - function inferOperatorReturnType(operator)
  - function validateAST(root)
  - function findIncompleteNode(root)
  - function findRightmostLeafNode(root)
- path.js 导出：
  - function pushPath(stack, entry)
  - function popPath(stack)
  - function peekPath(stack)
  - function clearPath(stack)
  - function writeBackToParentSlot(parentNode, slot, newValueNode)
- operators.js 导出：
  - function getSmartOperators(typeName)
  - function getTextOperators()
  - function getEnumOperators(typeName)
  - function getOptionsForType(typeName, metadata)
- ui-main.js 导出：
  - function renderMainExpression(container, mainExpressionAST, editingPath, callbacks)
- ui-workspace.js 导出：
  - function renderWorkspace(container, workspaceAST, editingPath, callbacks)

## 迁移顺序（建议）
1) 建立模块骨架（本次提交）
2) 将路径与保存相关的纯函数抽到 path.js（最小侵入）
3) 将操作符/选项策略抽到 operators.js
4) 将 AST 纯工具抽到 core.js（保留 DslEditor 方法的薄封装）
5) 拆分 UI：
   - 将主表达式渲染移入 ui-main.js
   - 将工作区渲染移入 ui-workspace.js
6) 收敛 DslEditor.js，仅保留协调逻辑和事件桥接

## 测试与日志
- 每次迁移后保留现有日志，必要时增加关键路径快照
- 优先覆盖 (6)>5、逻辑链 &&/||、集合 listQuery 的用例

## 回滚策略
- 单次迁移仅移动小片段代码；若出现问题，优先回退至前一版本

## 风险与注意事项
- 双轨渲染（旧 renderOperatorNode 与 AST 渲染）混杂：迁移过程中要明确新旧路径，逐步淘汰旧渲染
- editingContext 已弃用：确保不引入旧逻辑

> 执行：立即创建模块骨架文件，后续按迁移顺序逐步推进。

