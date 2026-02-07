## ADDED Requirements
### Requirement: DslEditor Modular Boundaries
The system SHALL separate rendering, pipeline editing, and parsing/type-check logic into dedicated modules while preserving existing behavior.

#### Scenario: Rendering logic is isolated
- **WHEN** the DSL editor renders the main expression and workspace
- **THEN** rendering is delegated to dedicated UI modules

#### Scenario: Pipeline editing logic is isolated
- **WHEN** the user edits pipeline stages
- **THEN** stage state and editing behaviors are handled by the pipeline module

#### Scenario: Parsing and type checking logic is isolated
- **WHEN** the editor parses or validates expressions
- **THEN** parsing/type checking utilities are invoked from dedicated modules

### Requirement: Behavior Parity During Refactor
The system SHALL preserve existing DSL editor behaviors after modularization.

#### Scenario: Editing flow remains unchanged
- **WHEN** the user performs the same editing steps as before the refactor
- **THEN** the resulting DSL expression and UI feedback match the previous behavior

### Validation Checklist
- Main expression renders correctly and remains editable.
- Workspace editing path saves/returns without state loss.
- Pipeline editor supports stage add/edit/remove and inline editing flow.
- Favorites list loads, can create a favorite, and recent usage updates.
- Parse fallback enters raw-text mode when parse API fails.
- AST DTO mapping produces equivalent UI AST nodes for parsed expressions.
- Option provider returns correct options for empty, incomplete, and completed expressions (including event fields).
- AST editing flow handles create/extend/back/save without corrupting workspace state.
- UI feedback shows parse status and expression errors in the editor container.
- Main expression, workspace chips, and encapsulated capsules render with expected spacing/selection/highlight styles.
- Placeholder capsules and empty slots use the warning styling and remain clickable.
- Pipeline block and pipeline editor preserve layout, badges, and button styles.
- Favorites toolbar/menu preserve layout, button states, and list hover styles.
