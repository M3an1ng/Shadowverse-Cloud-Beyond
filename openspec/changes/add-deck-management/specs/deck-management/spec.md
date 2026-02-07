## ADDED Requirements

### Requirement: User Deck Lifecycle
The system SHALL allow a user to create, list, view, rename, and delete decks scoped to that user.

#### Scenario: Create a deck
- **WHEN** a user creates a deck with a name and class
- **THEN** the system returns a new deck id and persists the deck metadata

#### Scenario: Delete a deck
- **WHEN** a user deletes a deck they own
- **THEN** the deck is no longer returned in the user's deck list

### Requirement: Deck Composition Rules
The system SHALL enforce that a deck contains at most 40 cards, each card has at most 3 copies, and every card is either Neutral or matches the deck's class.

#### Scenario: Reject too many copies
- **WHEN** a user attempts to save a deck with more than 3 copies of a card
- **THEN** the system rejects the save with a validation error

#### Scenario: Reject invalid class
- **WHEN** a user attempts to add a card that does not match the deck class and is not Neutral
- **THEN** the system rejects the change with a validation error

#### Scenario: Reject oversized deck
- **WHEN** a user attempts to save a deck with more than 40 cards
- **THEN** the system rejects the save with a validation error

#### Scenario: Allow smaller deck
- **WHEN** a user saves a deck with fewer than 40 cards
- **THEN** the system accepts the save

### Requirement: Deck Persistence
The system SHALL persist the deck's card list and metadata bound to UserId, and return stable deck ids for subsequent edits.

#### Scenario: Save and reload a deck
- **WHEN** a user saves a deck and later fetches its details
- **THEN** the system returns the same deck id and the saved card list

### Requirement: Deck Code Import and Export
The system SHALL export a deck code for a saved deck and SHALL import a deck code into a new or existing deck with validation.

#### Scenario: Export and import round-trip
- **WHEN** a user exports a deck code and imports it back
- **THEN** the resulting deck composition matches the original

#### Scenario: Invalid deck code
- **WHEN** a user imports a malformed or invalid deck code
- **THEN** the system rejects the request with a validation error

### Requirement: Guest Deck Session Storage
The system SHALL treat unauthenticated players as guests and SHALL store their deck data only in temporary session storage without persistence.

#### Scenario: Guest creates a deck
- **WHEN** a guest player creates or edits a deck
- **THEN** the deck is stored only in a temporary session scope and is not written to persistent storage

#### Scenario: Guest session ends
- **WHEN** the guest session ends or the server restarts
- **THEN** the guest deck data is no longer available

### Requirement: Default Deck Bootstrap
The system SHALL provide a deck bootstrap operation that creates a default deck for a user or guest when they have no decks, using the server-defined starter deck list.

#### Scenario: Bootstrap creates a default deck
- **WHEN** a user or guest without any decks requests deck bootstrap
- **THEN** the system creates a default deck and returns it in subsequent deck listings

#### Scenario: Bootstrap is idempotent
- **WHEN** a user or guest already has at least one deck and requests deck bootstrap
- **THEN** the system does not create an additional default deck

### Requirement: Deck Availability Check
The system SHALL expose whether a player has at least one deck so the client can gate match entry.

#### Scenario: No decks available
- **WHEN** the client queries deck availability for a player with zero decks
- **THEN** the system indicates that the player has no available decks

### Requirement: Deck Environment Selection
The system SHALL require a deck environment and class to be selected before a deck can be edited and SHALL persist the environment metadata with the deck.

#### Scenario: Create deck with environment and class
- **WHEN** a user creates a deck and provides an environment and class
- **THEN** the system persists both values and returns them in deck details

#### Scenario: Missing environment on create
- **WHEN** a user attempts to create a deck without an environment
- **THEN** the system rejects the request with a validation error

### Requirement: Environment-Aware Card Pool
The system SHALL restrict the deck editor card pool to cards allowed by the deck environment and to Neutral or the deck class.

#### Scenario: Default environment allows all sets
- **WHEN** a deck uses the default environment
- **THEN** the editor may show cards from any card set, filtered to Neutral or the deck class
