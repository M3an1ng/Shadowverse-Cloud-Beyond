## ADDED Requirements

### Requirement: Match Deck Selection
The system SHALL require each human player to select a saved deck before a match can start, and SHALL validate that the deck belongs to the requesting UserId.

#### Scenario: Multiplayer deck selection
- **WHEN** host and guest each submit a deck selection
- **THEN** the match is eligible to start

#### Scenario: Invalid ownership
- **WHEN** a player submits a deck id that is not owned by their UserId
- **THEN** the system rejects the selection

### Requirement: Single-Player AI Deck Selection
The system SHALL allow the host to select a deck for the AI opponent when starting a single-player match.

#### Scenario: Host selects both decks
- **WHEN** the host submits a player deck and an AI deck selection
- **THEN** the match is eligible to start

### Requirement: Game State Deck Initialization
The system SHALL build in-game decks from the selected deck lists when the match starts, and SHALL bind them to the in-game PlayerId values.

#### Scenario: Start a match with selected decks
- **WHEN** a match starts with valid deck selections
- **THEN** each player receives a shuffled in-game deck matching their selected list
