# Design: Metadata-Driven Ability Editing (Safe-by-Default)

## 1. 元信息来源（现状）
能力编辑器通过 `GET /api/AbilityEditorMetadata` 获取元信息聚合（effects/events/dsl/queryOperations）。
- Effect UI metadata：参数名、显示名、控件类型、默认值、范围（min/max）
- DSL metadata：`contextObjects`、`typeMetadata`（DslExposable 属性）、函数签名（name/returnType/parameters）
- Event metadata：每个事件类型的参数列表（Input/Output scope）

## 2. 核心 UX 目标
1) **让用户“少选/少写”**：能点选就不让手写；能自动推断就不让用户选类型。
2) **让错误“早失败”**：保存前阻断不可执行配置，并给出明确修复建议。
3) **让能力“可解释/可预览”**：能看到会对谁生效、会产生什么效果（至少做到静态预览与表达式可读化）。

## 3. 智能化适配点（基于元信息）
### 3.1 效果参数控件选择（Effect UI metadata）
- 按 `controlType` 直接渲染：Checkbox/NumberStepper/TextInput/TextArea/Dropdown/TargetSelector/ValueConfigEditor。
- `ValueConfigEditor` 的 `expectedType` MUST 来自“参数类型”，而不是硬编码。

### 3.2 DSL 结构化编辑（DSL metadata）
- 选择函数时应生成“函数调用节点”，为每个参数提供槽位（参数名/类型/描述来自元信息）。
- 每个槽位携带 `expectedType`，用于限制可选项与类型校验。

### 3.3 触发事件参数访问（Event metadata）
- UI 不展示 `eventInt(...)` 等内部函数名；改为“可选字段列表”：
  - `触发事件 → 输入参数`（默认可读）
  - `触发事件 → 输出参数`（仅当语义允许，例如 After/On）
- 选择字段后，编辑器插入对应 DSL 片段（实现细节可为函数调用或属性访问，但不在 UI 暴露）。

## 4. 可靠性门槛（保存前校验）
保存能力配置前，前端与后端都需要一致的校验策略：
- 必填字段缺失（效果参数/目标规则/事件类型等）
- DSL 语法错误、类型不匹配、引用不存在的字段/函数
- 事件参数 scope 违规（例如在不允许的时点读取 output）

## 5. 增量后端扩展（避免前端猜测）
Effect UI metadata 当前缺少参数类型信息，导致前端只能猜 `expectedType`。
建议增量字段：
- `EffectParameterMetadataDto.TypeName`（参数真实 CLR/DSL 类型）
- 若 `controlType=Dropdown`：提供 `EnumName` 或 `OptionsProviderKey`（前端可加载选项）

## 6. DslEditor 架构演进方案（严谨、可增量落地）
本节为 `src/components/DslEditor.js` 的演进设计，目标是在不“一次性重写”的前提下，逐步达成：
- **Slot-based + type-aware**：编辑单位从“最后一个节点推断”升级为“槽位（slot）驱动”的严格约束编辑。
- **元信息驱动**：函数签名、属性/字段列表、事件字段、集合 inner type 等来自 metadata，而不是前端硬编码/猜测。
- **确定性与可回溯**：表达式的解析/序列化可预测；校验结果可定位到具体槽位，AbilityEditor 可在保存前阻断。

### 6.1 现状问题（作为演进驱动力）
当前 `DslEditor` 的代码形态更接近“单体组件”：状态机、AST、类型推断、选项生成、UI 渲染混杂在一个类中，导致：
- 很难实现“函数调用节点 + 参数槽位”这类结构化编辑（只有 function option，但缺少 args 槽位的一等建模）。
- `expectedType` 主要是编辑器级别（global），不足以表达“参数级/槽位级 expectedType”。
- 集合类型/链式操作的类型精度不足（例如使用 `List` 占位而非 `List<T>`），进而影响校验可靠性。
- 校验缺少结构化定位，AbilityEditor 很难给出“可操作且可定位”的错误提示。

### 6.2 目标架构（分层 + 可测试核心）
保持外部 API（`DslEditor.render(container, expression, onChange, options)`）不变；内部演进为四层：

1) **Domain（DSL AST）**
- 仅负责节点结构、clone、遍历、`toExpression()`（序列化）。
- 节点需要可承载“缺失槽位”的占位符（Placeholder），以便 UI 能展示“这里需要一个 xxx 类型”。

2) **Typing（类型归一 + 槽位模型 SlotModel）**
- 负责 `TypeName` 归一化（`Nullable<T>`、`List<T>/IEnumerable<T>`、数值类型归一等）。
- 负责对 AST 做 returnType 推断、对每个可编辑槽位计算 `expectedType`。
- 对集合链式操作提供规则化 typing：
  - `List<T>.query(predicate: Boolean, scope=self:T)`
  - `List<T>.orderBy(key: K, scope=self:T)`
  - `List<T>.take(count: Int32|Number)`

3) **Editing（选择/槽位定位 + AST 变换操作）**
- 所有编辑都定位到一个 `SlotRef`（槽位引用），而不是“当前最后节点”：
  - `SlotRef = { parentNodeId, slotKey, index? }`
- 所有 UI 操作都会生成一个 **可测试的 AST 变换**（operation），例如：
  - `ReplaceSlot(slotRef, newNode)`
  - `WrapSlotWithBinaryOp(slotRef, operator, rightPlaceholderExpectedType)`
  - `CreateFunctionCall(name) + FillArg(i, node)`
  - `EnterSubEditor(slotRef)` / `ExitSubEditor(save|discard)`

4) **UI（渲染与交互）**
- UI 只消费：当前 AST、当前 SlotRef、当前 SlotModel、候选 option 列表、validation issues。
- UI 不直接推断类型/拼表达式字符串；UI 只负责触发 operation 并渲染结果。

### 6.3 AST 节点集（演进目标）
为满足 OpenSpec “结构化节点 + 槽位”要求，AST 至少需要：
- `LiteralNode(value, typeName)`：静态常量（数值/文本/布尔/枚举等）
- `ContextRefNode(name, typeName)`：上下文对象（如 card/target/player/self）
- `PropertyAccessNode(receiver, propertyName, returnType)`：属性访问（可扩展为链式）
- `BinaryOpNode(operator, left, right, returnType)`：二元操作符
- `FunctionCallNode(name, args[], returnType)`：函数调用（参数槽位数组）
- `ListQueryNode(source, predicate)` / `ListSortNode(source, keySelector)` / `ListTakeNode(source, count)`
- `PlaceholderNode(expectedType)`：空槽位占位（用于“未完成但可编辑”的表达式）

兼容策略：
- 初期允许 `RawTextNode(text)` 作为降级节点（解析失败或尚未支持的语法），但该节点 MUST 在 UI 中以“不可结构化/需要手写”的明确状态呈现，并在保存前校验为 error（除非显式开启 allowUnsafeText）。

### 6.4 元信息输入与上下文（EditorOptions）
`DslEditor` 应接受结构化 options（逐步引入，不强制一次性到位）：
- `expectedType: TypeName`（外部期望类型，作为根槽位 expectedType）
- `contextOverrides?: [{ name, displayName, typeName }]`（例如 `.query/.orderBy` 的 `self:T`）
- `triggeringEventContext?: { eventType, timing, relationType }`（用于事件字段选择器与 scope 校验）
- `allowUnsafeText?: boolean`（默认 false）

### 6.5 结构化校验输出（供 AbilityEditor 汇总）
除 `onChange(expression)` 外，`DslEditor` 内部应形成结构化校验结果（后续可扩展为对外回传）：
- `ValidationIssue = { severity: 'error'|'warn'|'info', message, slotRef?, nodePath?, code? }`
- 关键场景：
  - `expectedType` 不兼容（slot 级别）
  - 引用不存在的函数/字段/属性
  - 事件字段 scope 违规
  - RawTextNode 存在但 allowUnsafeText=false

### 6.6 解析/序列化（确定性策略）
分两阶段推进：
1) **短期（兼容优先）**：支持“可解析的子集”，其余降级为 `RawTextNode`；编辑器仍能渲染/提示并阻断保存。
2) **中期（确定性）**：实现对目标 AST 节点集的确定性 `parseExpression` 与 `toExpression`：
   - 保障 `parse(toExpression(ast))` 结构等价（round-trip），并用于测试基线。

### 6.7 增量落地路线图（不破坏现有体验）
**Phase 0：边界清晰化（不改行为）**
- 将 `dsl_editor/*` 模块作为“唯一 AST/typing/ops 入口”，`DslEditor.js` 作为适配层调用。
- 明确并收敛类型系统：统一使用 `TypeName`（含泛型）作为内部类型表示。

**Phase 1：SlotRef + Placeholder（提升严谨性）**
- 将当前“编辑路径栈”正式收敛为 `SlotRef`。
- 引入 `PlaceholderNode(expectedType)`；UI 用 placeholder 明确提示“此处需要 xxx”。
- 让二元操作符、list 操作的子表达式都走 slot-based 选项生成（不再依赖“last node”猜测）。

**Phase 2：FunctionCallNode（满足函数参数槽位）**
- 函数选择生成 `FunctionCallNode`，并为每个参数创建 placeholder slot（expectedType 来自 metadata）。
- 支持对任意参数槽位进入子编辑（EnterSubEditor(slotRef)）。

**Phase 3：TriggeringEvent 字段选择器（不暴露内部函数）**
- 在 options 层提供 `EventFieldOption`，渲染为“字段选择器”。
- 选择字段后插入 `PropertyAccessNode/FieldRefNode`（实现细节隐藏），并参与 type/scope 校验。

**Phase 4：严格校验与 AbilityEditor 汇总**
- `DslEditor` 产出的 ValidationIssue 可被 `AbilityEditor` 汇总，形成保存前一致门槛与定位信息。

**Phase 5：确定性 round-trip 与移除降级路径**
- 覆盖常见表达式子集的确定性解析/序列化。
- 将 `RawTextNode` 降级路径收敛为“只读/禁止保存”，并逐步减少出现概率。

### 6.8 验收标准（对齐 OpenSpec）
- 用户在任意槽位只能看到“类型兼容”的可选项；放置不兼容表达式会被阻止或明确标红。
- 函数调用必须以参数槽位形式编辑，不需要用户手写参数分隔/括号。
- `.query/.orderBy` 子表达式自动获得 `self:T` 上下文，且类型/作用域校验可在保存前阻断。
- AbilityEditor 能定位到具体 “condition/target filter/effect param” 的 DSL 错误并阻断保存。
