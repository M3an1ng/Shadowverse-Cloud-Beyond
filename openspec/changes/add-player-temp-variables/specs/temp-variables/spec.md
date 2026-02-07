## ADDED Requirements
### Requirement: Player temporary counters
The system SHALL provide a temporary counter container on PlayerState with read/write/add/consume operations.

#### Scenario: Read and write player counters
- **WHEN** an effect or system action sets or adds a player counter
- **THEN** the updated value is available through PlayerState accessors and DSL exposure

### Requirement: Pre-registered player counters
The system SHALL pre-register the player counters combo, graveyard, and bloodpool with default value 0.

#### Scenario: New player state initializes counters
- **WHEN** a PlayerState is created for a match
- **THEN** combo, graveyard, and bloodpool counters exist and read as 0

### Requirement: Modify variable effect
The system SHALL provide an effect that can add, set, or consume a named counter on player or card targets.

#### Scenario: Consume a player counter
- **WHEN** the effect consumes a counter and the available value is sufficient
- **THEN** the counter is reduced by the requested amount and the effect succeeds

#### Scenario: Consume fails on insufficient value
- **WHEN** the effect consumes a counter but the available value is insufficient
- **THEN** the effect fails and no counter mutation is applied