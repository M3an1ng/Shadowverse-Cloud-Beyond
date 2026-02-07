## ADDED Requirements
### Requirement: Card pack autocomplete in editor
The system SHALL provide autocomplete suggestions for card pack input in the card editor based on existing pack definitions.

#### Scenario: Autocomplete suggestions
- **WHEN** a user types a pack name in the card editor
- **THEN** the editor shows suggestions matching existing pack names, abbreviations, or pack codes

### Requirement: Auto-register new card packs
The system SHALL auto-register a new card pack definition when a saved card references an unknown pack name.

#### Scenario: New pack registration
- **WHEN** a card is saved with a pack name that matches no existing definition
- **THEN** a new card pack definition is created with a blank abbreviation and a default Open environment list, and linked to the card

### Requirement: Card pack environment list
Card pack definitions SHALL store one or more environments as a list.

#### Scenario: Environment list default
- **WHEN** a card pack definition does not specify environments
- **THEN** the environment list defaults to Open

### Requirement: Default sequence allocation for auto-registered packs
Auto-registered packs SHALL use region 0 and allocate sequence numbers starting from 2 (with 0 and 1 reserved).

#### Scenario: Auto-registration sequence assignment
- **WHEN** a new pack is auto-registered
- **THEN** the assigned pack code uses region 0 and the lowest available sequence >= 2
