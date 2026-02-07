## ADDED Requirements

### Requirement: Parameter DSL Input Mode
The system SHALL allow DSL input for all effect parameters and enforce mutual exclusion with structured inputs.

#### Scenario: Per-parameter mode toggle
- **WHEN** the user switches a parameter to DSL mode
- **THEN** the structured input is hidden and ignored for execution

#### Scenario: Mode switch preserves last values
- **WHEN** the user switches between structured and DSL modes
- **THEN** the last value for each mode is preserved for future toggles

### Requirement: Parameter Metadata DSL Flags
The system SHALL expose DSL capability metadata for each parameter.

#### Scenario: Frontend receives DSL flags
- **WHEN** parameter metadata is requested
- **THEN** AllowDsl, DslType, and DslMode are included in the response

### Requirement: DSL Type Validation
The system SHALL validate DSL output types against parameter types and block save on mismatch.

#### Scenario: Invalid DSL type
- **WHEN** a DSL expression returns a value incompatible with the parameter type
- **THEN** an error is shown and saving is prevented

### Requirement: Parameter UI Readability
The system SHALL improve parameter UI readability with clearer labels and help text.

#### Scenario: Dense parameter panels
- **WHEN** a user views a complex effect configuration
- **THEN** labels, descriptions, and spacing make parameters scannable and unambiguous