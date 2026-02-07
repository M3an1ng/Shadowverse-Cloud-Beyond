## ADDED Requirements

### Requirement: Pipeline Editor Entry for Collection Nodes
The DSL editor SHALL expose a pipeline editor when the selected expression represents a CardQuery or collection pipeline.

#### Scenario: Open pipeline editor from a query capsule
- **WHEN** the user clicks a CardQuery capsule or placeholder in the completed expression
- **THEN** the editor shows the pipeline stages for that node and keeps the editing path bound to the same parent slot

### Requirement: Stage List Editing
The pipeline editor SHALL allow adding, removing, reordering, and editing stages using the same capsule UI semantics.

#### Scenario: Reorder stages
- **WHEN** the user drags a stage to a new position
- **THEN** the underlying query AST order updates and the completed expression preview reflects the change

### Requirement: Round-trip with CardQuery DSL
The system SHALL round-trip between pipeline AST and CardQuery DSL syntax without loss of supported information.

#### Scenario: Load chain DSL into pipeline
- **WHEN** a DSL string containing `.query/.orderBy/.take` is loaded
- **THEN** the editor parses it into pipeline stages, or falls back to raw-text mode if parsing fails

### Requirement: Stage Schema Alignment
The pipeline editor SHALL align stage fields with the backend CardQuery operations.

#### Scenario: Map stage fields to backend operations
- **WHEN** the user edits a Filter/Sort/Select stage
- **THEN** the editor updates the corresponding `FilterOperation`, `SortOperation`, or `SelectionOperation` fields without changing semantics

### Requirement: Stage Type Context
The pipeline editor SHALL compute and display the expected type for each stage output and use it for capsule validation.

#### Scenario: Stage type feedback
- **WHEN** a stage output type does not match the next stage input
- **THEN** the stage shows a mismatch indicator, and final validation blocks saving the completed expression

### Requirement: Unsupported Stage Preservation
The system SHALL preserve unsupported or unknown query segments as raw stages that are displayed but not edited.

#### Scenario: Unsupported segment round-trip
- **WHEN** a query segment cannot be mapped to a supported stage
- **THEN** the pipeline shows a raw stage and serialization keeps the original text intact

### Requirement: Select Strategy Constraints
The pipeline editor SHALL constrain selection strategies to those supported by the current DSL serializer.

#### Scenario: Prevent unsupported strategy serialization
- **WHEN** the user selects a strategy that has no DSL mapping
- **THEN** the editor blocks serialization or records the stage as raw text

### Requirement: Post-Process Stage Guardrail
The pipeline editor SHALL disable or mark PostProcess stages as read-only until a DSL mapping exists.

#### Scenario: PostProcess stage without DSL support
- **WHEN** the user attempts to add a PostProcess stage
- **THEN** the editor shows it as read-only or requires raw text input
