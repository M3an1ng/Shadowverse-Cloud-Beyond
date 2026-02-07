## ADDED Requirements
### Requirement: Card pack identifiers
The system SHALL store card pack definitions with Region (0-9), Sequence (0-99), Abbreviation, and DeckEnvironment, and SHALL expose a 3-digit PackCode derived from Region and Sequence.

#### Scenario: Pack code validation
- **WHEN** a card pack definition is created or updated
- **THEN** Region and Sequence MUST be within range and PackCode MUST be unique

### Requirement: Card definitions reference pack definitions
The system SHALL associate each card definition with a card pack definition as the source of truth for pack metadata.

#### Scenario: Compatibility for card_set
- **WHEN** a card definition is returned via API
- **THEN** the card_set value SHALL be derived from the linked pack definition name

### Requirement: Pack environment availability
The system SHALL scope card packs to DeckEnvironment for format-aware availability.

#### Scenario: Environment-filtered availability
- **WHEN** a client requests pack definitions for a specific environment
- **THEN** only packs matching that environment are returned
