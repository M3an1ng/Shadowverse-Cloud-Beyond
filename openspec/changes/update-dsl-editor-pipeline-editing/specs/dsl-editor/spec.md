## ADDED Requirements
### Requirement: Pipeline Stage Editing Uses Node-Based DSL
The system SHALL provide pipeline stage editing through the DSL node editor and SHALL NOT require direct free-text input for Filter, Sort, or Select stages.

#### Scenario: Edit filter stage with node selection
- **WHEN** the user clicks “编辑” on a Filter stage
- **THEN** the system shows the DSL node editor for building the filter expression
- **AND** the user selects nodes to complete the expression without typing raw text

### Requirement: Pipeline Stage Defaults and Strategies
The system SHALL guide Filter and Sort stages with a self context, and the Select stage SHALL restrict strategies to random/first/last.

#### Scenario: Filter/Sort provide self guidance
- **WHEN** the user opens a Filter or Sort stage editor
- **THEN** the available nodes are scoped to self context for the collection item

#### Scenario: Select stage restricts strategy
- **WHEN** the user edits a Select stage
- **THEN** the strategy options are limited to random, first, and last

### Requirement: Incomplete Pipeline Stages Are Not Persisted
The system SHALL omit incomplete pipeline stages from the workspace/main expression output.

#### Scenario: Incomplete stage is ignored on save
- **WHEN** the user adds a pipeline stage but leaves its expression empty
- **THEN** saving the workspace does not write the stage into the final DSL expression

### Requirement: Empty Expression Shows Chinese Type Hint
The system SHALL display Chinese type hints for all empty expressions and placeholders, using a hint style to indicate pending completion.

#### Scenario: Empty placeholder shows type hint
- **WHEN** the main expression or slot is empty
- **THEN** the placeholder shows the expected type in Chinese mapping with hint styling

### Validation Checklist
- Filter/Sort/Select stages open the node editor (no raw-text input) and keep inline editing in the pipeline panel.
- Filter/Sort stages scope options to self; Select exposes strategy buttons (first/last/random) and persists selection.
- Empty pipeline stage does not appear in the emitted DSL expression after save.
- Empty main/slot placeholders show Chinese type hints with hint styling.
