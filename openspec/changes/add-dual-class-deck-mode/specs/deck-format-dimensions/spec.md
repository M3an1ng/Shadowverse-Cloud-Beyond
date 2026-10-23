## ADDED Requirements

### Requirement: Deck format dimensions are independent
The system SHALL model deck format as two independent dimensions: `environment` (card-pool legality) and `mode` (construction/match rules). Deck persistence and API contracts MUST carry both dimensions.

#### Scenario: Create deck with explicit environment and mode
- **WHEN** a client creates a deck and provides both `environment` and `mode`
- **THEN** the system persists both values and returns both values in deck detail responses

#### Scenario: Legacy deck defaults are preserved
- **WHEN** a pre-existing deck record does not contain mode metadata
- **THEN** the system treats it as `Standard` mode under existing environment defaults without breaking deck retrieval

### Requirement: Validation uses environment-plus-mode context
Deck legality validation SHALL resolve rules by the tuple `(environment, mode)` rather than environment-only checks.

#### Scenario: Same environment, different mode, different result
- **WHEN** the same card list is validated under the same environment but different modes
- **THEN** the system applies the mode-specific rule set and MAY return different validation outcomes

### Requirement: Deck API contracts expose secondary class only when mode requires it
Deck APIs SHALL support an optional `secondaryClass` field and MUST require it only for modes that define secondary-class construction.

#### Scenario: Standard mode ignores secondary class
- **WHEN** a deck is created or updated in `Standard` mode
- **THEN** the system does not require `secondaryClass` and stores no effective secondary-class assignment

#### Scenario: Dual-class mode requires secondary class
- **WHEN** a deck is created or updated in `DualClass` mode
- **THEN** the system requires `secondaryClass` and rejects requests missing it
