## ADDED Requirements
### Requirement: Pipeline Stage Editor Supports Full DSL Editing
The system SHALL provide main-editor-level expression construction capabilities in pipeline stage child editors.

#### Scenario: Build complex stage expression
- **WHEN** the user edits a pipeline stage in the modal
- **THEN** the child editor exposes workspace and full expression display
- **AND** the user can compose nested operators/functions without lightweight limitations

### Requirement: Self Root Is Stable For Scoped Pipeline Stages
The system SHALL keep `self` as a persistently accessible context object in scoped pipeline stage editing when configured.

#### Scenario: Self context is always available in options
- **WHEN** a scoped stage editor (Filter/Sort/Distinct/Select) opens
- **THEN** context options include `self` even if `contextOverride` is reset by re-render

#### Scenario: Clear keeps self accessibility
- **WHEN** the user clears the scoped stage workspace
- **THEN** the editor may reset to empty placeholder
- **AND** `self` remains selectable as a context source

### Validation Checklist
- 管线阶段子编辑器可显示完整表达式并支持复杂构建。
- scoped 阶段上下文列表中始终可选 `self`。
- 清空后 `self` 仍然可访问，不依赖当前 `contextOverride`。
