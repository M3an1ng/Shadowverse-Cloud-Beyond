## ADDED Requirements

### Requirement: Effect UI Metadata MUST Include Parameter TypeName
后端暴露给能力编辑器的 Effect UI metadata MUST 包含每个参数的 `TypeName`（用于前端决定控件与 DSL expectedType），前端 MUST NOT 通过猜测或 `defaultValue` 类型推断来决定参数类型。

#### Scenario: ValueConfigEditor receives the correct expected type
- **GIVEN** an effect parameter is declared as an integer
- **WHEN** the editor renders that parameter
- **THEN** the metadata MUST include `TypeName = Int32` (or normalized numeric type)
- **AND** the Formula DSL editor MUST set `expectedType` to the same type

### Requirement: Dropdown Parameters MUST Provide Option Source
当参数 `controlType = Dropdown` 时，元数据 MUST 提供选项来源（`EnumName` 或 `OptionsProviderKey`），以便前端无需硬编码即可渲染下拉选项。

#### Scenario: Dropdown renders without hardcoding
- **GIVEN** a dropdown parameter
- **WHEN** the editor loads ability editor metadata
- **THEN** the parameter metadata MUST specify an option source identifier
- **AND** the editor MUST be able to render options using that identifier

