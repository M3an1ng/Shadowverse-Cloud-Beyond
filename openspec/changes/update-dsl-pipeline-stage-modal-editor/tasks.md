## 1. Implementation
- [x] 1.1 保留管线阶段列表结构，移除行内展开编辑面板的渲染路径。
- [x] 1.2 在 `ui-pipeline.js` 新增阶段编辑子 Modal 渲染（含标题、主体、应用/取消按钮）。
- [x] 1.3 将 “取消/Esc/遮罩点击” 统一收敛到 `cancelPipelineStageEdit`。
- [x] 1.4 确保 Modal 内仍复用现有 `renderPipelineStageInlineEditor` 与类型约束逻辑。
- [x] 1.5 在 `DslEditor.css` 添加 Modal 样式层，避免影响现有管线列表视觉。

## 2. Verification
- [x] 2.1 验证点击“编辑”后只打开 Modal，不再占用主工作区内联空间。
- [x] 2.2 验证 Apply 只在校验通过后写回表达式，Cancel/Esc/遮罩点击均不写回。
- [x] 2.3 验证 Filter/Sort/Take/Distinct/Select 阶段在 Modal 内均可编辑，且摘要及时更新。
- [x] 2.4 补充或更新测试，覆盖阶段编辑打开/关闭与提交路径。
