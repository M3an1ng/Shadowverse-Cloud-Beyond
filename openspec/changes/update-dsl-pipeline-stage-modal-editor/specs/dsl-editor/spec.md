## MODIFIED Requirements
### Requirement: Pipeline Stage Editing Uses Node-Based DSL
The system SHALL provide pipeline stage editing through the DSL node editor and SHALL open stage editing in a child modal window instead of inline expansion in the pipeline list.

#### Scenario: Open stage editor in child modal
- **WHEN** the user clicks “编辑” on a pipeline stage
- **THEN** the system opens a child modal DSL editor for that stage
- **AND** the pipeline list layout remains unchanged

#### Scenario: Apply stage edit from modal
- **WHEN** the user clicks “应用” in the stage modal
- **THEN** the system validates the stage expression and writes it back to the pipeline AST
- **AND** the modal closes after a successful apply

#### Scenario: Cancel stage edit from modal
- **WHEN** the user closes the modal via “取消”、`Esc`、or overlay click
- **THEN** the system discards draft stage edits
- **AND** the main workspace expression remains unchanged

## ADDED Requirements
### Requirement: Pipeline Stage Modal Accessibility
The system SHALL keep child modal editing keyboard-accessible and discoverable.

#### Scenario: Keyboard close behavior
- **WHEN** the stage modal is open and the user presses `Esc`
- **THEN** the modal closes and returns focus to the pipeline editor context

#### Scenario: Visual focus feedback
- **WHEN** users navigate modal controls via keyboard
- **THEN** interactive controls display visible focus indicators

### Validation Checklist
- 点击阶段“编辑”后，编辑器以子 modal 打开，阶段列表视觉结构不变。
- 阶段编辑仍使用 DSL 节点化编辑，不回退到自由文本模式。
- “应用”成功后更新表达式；“取消/Esc/遮罩点击”不更新表达式。
- Modal 内控件支持键盘可达与清晰焦点样式。
