## ADDED Requirements
### Requirement: Ability execution branch derives from AbilityType
The system SHALL derive an ability's execution branch from `AbilityType` rather than a separate `executionKind` field.

#### Scenario: Triggered ability executes effects
- **WHEN** an ability has `AbilityType` set to `Triggered`
- **THEN** the system executes the ability's effects

#### Scenario: Continuous ability executes auras
- **WHEN** an ability has `AbilityType` set to `Continuous`
- **THEN** the system applies or refreshes the ability's auras

#### Scenario: Activated ability executes effects
- **WHEN** an ability has `AbilityType` set to `Activated`
- **THEN** the system executes the ability's effects

### Requirement: Legacy executionKind fields are ignored
The system SHALL ignore any `executionKind` fields present in serialized ability configurations.

#### Scenario: Legacy JSON includes executionKind
- **WHEN** an ability JSON payload includes an `executionKind` field
- **THEN** deserialization succeeds and execution behavior is derived from `AbilityType`
