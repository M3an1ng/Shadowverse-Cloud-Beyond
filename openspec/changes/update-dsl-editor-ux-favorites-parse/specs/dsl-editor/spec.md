## ADDED Requirements

### Requirement: Single Workspace Editing
The DSL editor SHALL maintain exactly one workspace per editor instance and load the selected subexpression into that workspace based on the current editing path.

#### Scenario: Edit subexpression from completed expression
- **WHEN** the user clicks an editable subexpression in the completed expression area
- **THEN** the workspace loads that subexpression, the editing path updates to the corresponding parent slot, and saving writes back to the same slot

### Requirement: Encapsulation and Placeholder Flow
The DSL editor SHALL create a placeholder for the right operand when a binary operator is added and enter edit mode for that placeholder.

#### Scenario: Add operator after completing left side
- **WHEN** the user adds a binary operator to a completed left expression
- **THEN** the right operand is created as an empty placeholder, and the workspace focuses on editing that placeholder

### Requirement: Per-layer Type Indicators
The DSL editor SHALL display a √/× indicator for each encapsulated expression based on the expected type for that layer.

#### Scenario: Type mismatch indicator and final validation
- **WHEN** an encapsulated expression’s actual type does not match its expected type
- **THEN** the capsule shows a × with a mismatch tooltip, and final step/save validation blocks progression until resolved

### Requirement: Chinese Readable Display
The DSL editor SHALL present a Chinese readable preview using metadata display names for context objects, properties, and functions.

#### Scenario: Render readable preview
- **WHEN** a DSL expression is displayed in the readable preview
- **THEN** the preview uses displayName/description metadata instead of raw DSL identifiers by default

### Requirement: Favorite Expressions
The system SHALL allow users to save, search, and insert favorite DSL expressions with type-aware filtering.

#### Scenario: Insert a favorite expression
- **WHEN** the user searches favorites within the editor and selects a saved expression
- **THEN** the expression loads into the workspace and is validated against the current expected type

### Requirement: Reverse Parsing to UI AST
The system SHALL provide a reverse parsing capability that converts DSL strings into UI-editable AST structures.

#### Scenario: Parse existing DSL for editing
- **WHEN** a DSL string is loaded into the editor
- **THEN** the system attempts to parse it into a UI AST, and if parsing fails, falls back to a raw-text editable state
