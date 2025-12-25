## ADDED Requirements
### Requirement: TriggeringEvent 为 DSL 的唯一可靠源事件
在能力执行期间，DSL MUST 通过 `context.triggeringEvent` 访问触发事件信息；TriggeringEvent MUST 在 Before/On/After 全流程稳定可读，并携带该事件的输入/输出参数与已解析 Source/Target。

#### Scenario: 伤害触发能力读取伤害数值
- **WHEN** 一次伤害事件触发某个能力
- **THEN** 该能力在 DSL 中可通过 TriggeringEvent 读取伤害数值，并据此判断条件

### Requirement: DSL 事件参数访问 API（Input/Output）
系统 MUST 为 DSL 提供统一的“事件参数访问”API，用于读取 TriggeringEvent 的输入/输出参数；API MUST 支持类型安全变体（如 int/bool/string/guid），并在参数缺失或类型不匹配时返回可诊断错误或空值（按 DSL 约定）。

#### Scenario: 按名称读取输出参数
- **WHEN** 一段 After DSL 需要读取触发事件的输出参数（如抽到的牌 ID）
- **THEN** DSL 可通过事件参数访问 API 按名称读取该输出字段

### Requirement: 事件参数元数据用于编辑器自动提示
系统 MUST 暴露每个事件类型的参数元数据（含 DisplayName/Description/TypeName），并明确标注参数归类为 Input / Output / Both。前端编辑器 MUST 能基于该元数据按事件类型给出可访问参数提示，减少误用与学习成本。

#### Scenario: 编辑器按事件类型展示参数列表
- **WHEN** 用户在编辑器中选择一个事件类型作为触发条件
- **THEN** 编辑器可列出该事件可访问的输入/输出参数（含类型与描述），并用于自动补全/校验

