## Context
参数编辑器当前仅部分字段支持 DSL，导致输入方式不统一；且参数表单在复杂效果下可读性不足。

## Goals / Non-Goals
- Goals:
  - 所有参数类型都能选择 DSL 输入
  - 结构化输入与 DSL 互斥，切换不丢值
  - 参数 UI 更清晰、可读、易扫描
- Non-Goals:
  - 进行 DSL 性能优化（懒加载/虚拟化等）
  - 重写 DSL 解析器或更改 DSL 语法
  - 改变现有结构化控件的数据含义

## Decisions
- Decision: 在参数元数据中新增 AllowDsl/DslType/DslMode 字段
  - Reason: 统一前端渲染逻辑与类型校验依据
- Decision: DSL 与结构化互斥，保存时以当前模式为准
  - Reason: 降低歧义与执行优先级冲突
- Decision: 优化参数 UI 的层级与描述文案
  - Reason: 提升复杂效果下的可读性与编辑效率

## Risks / Trade-offs
- 风险: DSL 校验不一致导致运行期错误
  - 缓解: 统一类型转换规则并在保存时强制校验

## Migration Plan
- 新字段默认 AllowDsl=true，DslMode=Optional
- 现有数据视为结构化输入；切换到 DSL 时保留结构化值以便回退

## Open Questions
- 是否需要全局开关强制 DSL 模式用于高级用户工作流？
- 参数 UI 优化是否需要引入分组/折叠逻辑？
