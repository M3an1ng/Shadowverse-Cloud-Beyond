## ADDED Requirements

### Requirement: Tribe definitions are database-configurable
The system SHALL manage `tribe` definitions through persistent configuration rather than hard-coded normal-tribe lists.

#### Scenario: System loads configured tribe definitions
- **WHEN** the game data provider refreshes runtime metadata
- **THEN** the runtime SHALL load enabled `tribe` definitions from persistent storage
- **AND** each definition SHALL expose a canonical name, zero or more aliases, and an optional special behavior flag

#### Scenario: Newly submitted normal tribe is registered
- **WHEN** a card save, seed import, or tribe-granting effect introduces a non-reserved `tribe` name that is not yet configured
- **THEN** the system SHALL normalize the input string
- **AND** the system SHALL register it as a normal configured `tribe`

### Requirement: Tribe values are normalized before persistence and runtime comparison
The system SHALL normalize `tribe` strings at write boundaries and runtime merge points so that semantically identical tribe values resolve to the same canonical string.

#### Scenario: Alias resolves to canonical tribe name
- **WHEN** a caller submits a `tribe` value that matches a configured alias
- **THEN** the persisted and runtime `tribe` value SHALL use the configured canonical name

#### Scenario: Duplicate tribe values collapse after normalization
- **WHEN** a card definition or card instance receives multiple `tribe` values that normalize to the same canonical name
- **THEN** the effective `tribe` list SHALL contain that canonical name only once

### Requirement: Special tribe behavior is centrally interpreted
The system SHALL interpret special `tribe` behavior through a shared tribe semantics layer instead of ad hoc string checks.

#### Scenario: Match-all tribe expands to current known normal tribes
- **WHEN** a configured special `tribe` is marked with match-all-known-tribes behavior
- **THEN** the semantics layer SHALL treat that `tribe` as matching every enabled normal `tribe` currently known to the registry

#### Scenario: Special tribe remains stored as its canonical string
- **WHEN** a card or effect adds a special `tribe`
- **THEN** the stored `tribe` list SHALL retain the canonical special `tribe` string
- **AND** the full expansion SHALL be applied only during semantic evaluation
