## Context
- DSL 编辑器已支持管线阶段列表与节点化阶段表达式编辑，但编辑容器目前嵌入在管线列表内。
- 管线编辑本质是“子任务”，内嵌在主工作区会造成层级混乱，尤其在复杂表达式下更明显。

## Goals / Non-Goals
- Goals:
  - 在不改变管线列表视觉结构的前提下，引入子 Modal 编辑体验。
  - 复用现有阶段编辑逻辑（draft、类型校验、apply/cancel）以降低回归风险。
  - 提升复杂阶段表达式可编辑性，并明确“正在编辑的阶段上下文”。
- Non-Goals:
  - 不重写 DSL 主编辑器架构。
  - 不改变管线阶段语义（阶段顺序、插入/删除/移动规则保持不变）。
  - 不引入新的 DSL 语法能力。

## Decisions
- Decision: 阶段列表保持原样，编辑入口保持“编辑”按钮，仅更换编辑承载容器为 Modal。
  - Rationale: 最小化认知迁移，避免用户重新学习管线入口。

- Decision: 继续使用 `pipelineEditState` 作为单一编辑源，Modal 仅作为展示层。
  - Rationale: 现有提交/取消/类型检查逻辑可直接复用，减少状态分叉。

- Decision: Modal 关闭路径统一走 `cancelPipelineStageEdit`（除“应用”外）。
  - Rationale: 保证关闭行为可预测，防止中间态泄漏到主表达式。

- Decision: 增加可访问性交互约束：首焦点、Esc 关闭、遮罩关闭、明显焦点样式。
  - Rationale: 子窗口交互必须支持键盘用户，避免“编辑陷阱”。

## Interaction Flow
1. 用户点击某阶段“编辑”。
2. `editPipelineStage` 建立/刷新 `pipelineEditState`。
3. UI 检测到当前阶段处于编辑态，渲染子 Modal。
4. Modal 内渲染阶段特定控件（如 Take 策略、Sort 方向）与 DSL 子编辑器。
5. 用户点击“应用”：
   - 走 `commitPipelineStageEdit`。
   - 通过校验后写回 AST，关闭 Modal，刷新表达式。
6. 用户点击“取消”、按 `Esc` 或点击遮罩：
   - 走 `cancelPipelineStageEdit`，丢弃草稿并关闭 Modal。

## Risks / Trade-offs
- Risk: 频繁重渲染导致 Modal 内焦点抖动。
  - Mitigation: 仅在必要状态变更时刷新；关闭后清理编辑态。
- Risk: Modal 与现有全局弹层 z-index 冲突。
  - Mitigation: 在 `DslEditor.css` 内定义专用层级，确保高于管线区域与普通下拉菜单。
- Risk: 复杂表达式编辑器实例复用导致脏状态残留。
  - Mitigation: 明确 `pipelineEditState` 生命周期，切换阶段时重置草稿与编辑器引用。

## Migration Plan
1. 先替换 `ui-pipeline.js` 的行内编辑渲染为 Modal 渲染。
2. 再补齐 `pipeline-editor.js` 的关闭路径一致性与状态清理。
3. 调整样式并补充测试。

## Open Questions
- 是否需要在 Modal 标题区显示阶段表达式类型提示（如“期望 Boolean / NonEntity”）？
- 是否在后续版本支持“在 Modal 中切换上/下一阶段”快捷导航？
