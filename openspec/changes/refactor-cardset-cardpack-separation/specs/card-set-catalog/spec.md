## ADDED Requirements
### Requirement: Card set catalog
The system SHALL provide CardSetDefinition records with name, abbreviation, region, sequence, and environment list.

#### Scenario: Retrieve card set metadata
- **WHEN** a card set definition is loaded
- **THEN** it includes name, abbreviation, region, sequence, and environments

### Requirement: Card set code derived from region and sequence
Card sets SHALL expose a three-digit code derived from region and sequence.

#### Scenario: Compute set code
- **WHEN** a card set has region 0 and sequence 2
- **THEN** its code is "002"

### Requirement: Resolve card_set by name
Card definitions SHALL resolve card_set input by matching card set names (case-insensitive).

#### Scenario: Resolve set name on save
- **WHEN** a card is saved with card_set "Brave Vanguard"
- **THEN** the card links to the CardSetDefinition whose name equals "Brave Vanguard" (case-insensitive)

### Requirement: Auto-register new card sets
The system SHALL auto-register a CardSetDefinition when a saved card references an unknown card_set name.

#### Scenario: Auto-register unknown set
- **WHEN** a card is saved with an unrecognized card_set name
- **THEN** a new CardSetDefinition is created with Name set to the input string, an empty Abbreviation, and default environments

### Requirement: Card set input matches by name only
The card editor SHALL match card_set input against CardSetDefinition names only and SHALL NOT display abbreviation or code fields in the UI.

#### Scenario: Autocomplete by name
- **WHEN** a user types a card_set in the editor
- **THEN** suggestions match CardSetDefinition names only and show only the set name

### Requirement: Deck legality uses card set environments
Deck legality checks SHALL use card set environments, not card pack metadata.

#### Scenario: Illegal card for environment
- **WHEN** a deck is validated against an environment
- **THEN** a card is legal only if its CardSetDefinition includes that environment
