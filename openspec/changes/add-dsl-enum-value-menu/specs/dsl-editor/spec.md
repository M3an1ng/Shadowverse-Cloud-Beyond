## ADDED Requirements
### Requirement: Enum Value Menu
The DSL editor SHALL offer a selectable list of enum values when the expected type is an enum, and SHALL hide free-text input unless enum values are unavailable.

#### Scenario: Enum menu shown
- **WHEN** a DSL slot expects an enum type and metadata provides enum values
- **THEN** the editor shows those values as selectable options

#### Scenario: Manual input hidden
- **WHEN** a DSL slot expects an enum type and metadata provides enum values
- **THEN** the editor does not show manual free-text input for that slot

#### Scenario: Enum fallback
- **WHEN** a DSL slot expects an enum type but metadata has no enum values
- **THEN** the editor allows manual value input as a fallback without blocking the user

### Requirement: Enum Selection Inserts Literal
Selecting an enum value SHALL insert the value into the expression as a typed literal for that enum.

#### Scenario: Insert enum literal
- **WHEN** the user selects an enum value option
- **THEN** the expression updates with that enum value and retains the enum type
