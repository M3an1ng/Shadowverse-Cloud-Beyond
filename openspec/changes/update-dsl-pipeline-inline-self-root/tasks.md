## 1. Implementation
- [x] 1.1 管线阶段子编辑器切换为完整编辑模式，支持完整表达式显示。
- [x] 1.2 增加固定上下文能力（`fixedRootContext`），用于保持 `self` 始终可访问。
- [x] 1.3 上下文候选列表与上下文解析链路支持固定 `self` 上下文注入。
- [x] 1.4 保持初始化/清空行为为空占位，不强制写入 `self` 到表达式根。
- [x] 1.5 保证 `self` 可访问能力不依赖瞬时 `contextOverride`。

## 2. Verification
- [x] 2.1 `tests/dsl_editor_pipeline.test.js` 通过，覆盖固定 `self` 上下文可访问性。
- [x] 2.2 `tests/dsl_editor_interaction.test.js` 通过，确认主交互无回归。
