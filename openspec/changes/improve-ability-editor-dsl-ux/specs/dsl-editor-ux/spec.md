## ADDED Requirements

### Requirement: DSL Editor MUST Be Slot-Based And Type-Aware
DSL 可视化编辑器 MUST 以“结构化槽位”方式编辑表达式（函数参数/操作符左右操作数/集合操作的子表达式），每个槽位 MUST 携带 `expectedType` 并约束可选项，防止用户构造类型不匹配表达式。

#### Scenario: Function call creates typed parameter slots
- **WHEN** a user selects a DSL function with parameters
- **THEN** the editor MUST create a function-call node with one slot per parameter
- **AND** each slot MUST use the parameter `TypeName` as its expected type

#### Scenario: Type mismatch is prevented before saving
- **WHEN** a user tries to place a `String` expression into an `Int32` slot
- **THEN** the editor MUST block the action or show an error state
- **AND** the generated DSL MUST NOT be persisted while invalid

### Requirement: TriggeringEvent Params Must Be Exposed As Selectable Fields
编辑器 MUST 以“字段选择器”方式向用户展示 `TriggeringEvent` 的可访问参数（含中文名/描述/类型/Scope），并插入对应表达式；UI MUST NOT 暴露内部实现细节（如 `eventInt(...)`）。

#### Scenario: Designer selects a damage amount field
- **GIVEN** the triggering event is `Damage`
- **WHEN** the user builds a condition expression
- **THEN** the editor MUST list `Damage.amount` (Input) as a selectable field
- **AND** selecting it MUST insert an expression that evaluates to the correct numeric type

### Requirement: DSL Editor MUST Produce Slot-Addressable Validation Issues
DSL 编辑器 MUST 产生结构化的校验问题列表（severity + message + location），location MUST 能定位到具体槽位/节点，以便 AbilityEditor 在保存前聚合并提示用户。

#### Scenario: Type mismatch points to a specific slot
- **GIVEN** a function-call node has an `Int32` parameter slot
- **WHEN** the user places a `String` expression into that slot
- **THEN** the editor MUST surface an `error` issue
- **AND** the issue MUST identify the parameter slot location (e.g., slot index or slot ref)

#### Scenario: Unknown field produces actionable issue
- **WHEN** the user references a non-existent field/property in a structured expression
- **THEN** the editor MUST surface an `error` issue describing the missing field
- **AND** the issue MUST identify where the invalid reference is located

### Requirement: DSL Editor MUST Support Collection Sub-Editors With `self` Context
当编辑集合操作（如 `.query(...)` / `.orderBy(...)`）的子表达式时，编辑器 MUST 自动提供 `self:T` 上下文（`T` 为集合 inner type），并基于该上下文生成可选字段/函数与类型约束。

#### Scenario: Query predicate has self context and must be Boolean
- **GIVEN** the user is editing `List<CardInstance>.query(predicate)`
- **WHEN** the editor shows selectable context objects
- **THEN** the editor MUST include `self` as `CardInstance`
- **AND** the predicate editor MUST enforce `expectedType = Boolean`

### Requirement: DSL Parsing/Serialization MUST Be Deterministic For Supported Subset
对于编辑器声明支持的 DSL 子集，解析与序列化 MUST 是确定性的（round-trip 稳定），以确保可视化编辑不会在保存时产生不可预测的表达式漂移。

#### Scenario: Round-trip preserves structural meaning
- **GIVEN** an expression that is within the supported subset
- **WHEN** the editor parses it into an AST and serializes it back to a DSL string
- **THEN** the resulting DSL MUST re-parse to an equivalent AST
