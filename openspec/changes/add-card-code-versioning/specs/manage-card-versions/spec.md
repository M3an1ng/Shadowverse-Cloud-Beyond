## ADDED Requirements

### Requirement: Card Code Format
The system SHALL assign each card definition an 8-digit Code using decimal digits.

#### Scenario: Code generation
- **WHEN** a new card definition is created
- **THEN** the system SHALL generate a Code with digit 1 = group, digits 2-3 = pack sequence, digit 4 = craft, digit 5 = rarity, digit 6 = card type (GameEnum CardType), digit 7 = serial, digit 8 = 0.

#### Scenario: Card type mapping
- **WHEN** encoding card type into Code
- **THEN** the system SHALL use GameEnum CardType values and SHALL NOT encode evolved types.

### Requirement: Card Versioning
The system SHALL track versions using CardVersion per Code.

#### Scenario: Create new version
- **WHEN** a user saves a card as a new version
- **THEN** the system SHALL create a new CardDefinition with the same Code and CardVersion = max + 1.

#### Scenario: Immutable historical versions
- **WHEN** a request attempts to modify a non-latest version
- **THEN** the system SHALL reject the update.

### Requirement: Ability and Art Persistence
The system SHALL persist ability and art changes only when a card is saved.

#### Scenario: Draft edits without save
- **WHEN** a user edits abilities or art but does not save the card
- **THEN** no changes SHALL be persisted to card definitions, abilities, or art assets.

#### Scenario: Save applies to current version
- **WHEN** a card version is saved
- **THEN** the system SHALL persist abilities and art for that specific version only.

### Requirement: Deck Storage and Resolution
The system SHALL store decks by Code and resolve to the latest version at play time.

#### Scenario: Deck save
- **WHEN** a deck is saved
- **THEN** each deck entry SHALL store Code and count instead of CardDefinitionId.

#### Scenario: Resolve deck for play
- **WHEN** a deck is used to start a match
- **THEN** the system SHALL resolve each Code to the latest CardVersion and use those definitions for gameplay.

### Requirement: Deck Code Compatibility
The system SHALL support legacy deck codes and a new Code-based format.

#### Scenario: Decode legacy deck code
- **WHEN** a legacy deck code is decoded
- **THEN** the system SHALL map decoded ids to Code and resolve to the latest CardVersion.

#### Scenario: Encode new deck code
- **WHEN** a deck code is generated
- **THEN** the system SHALL encode Code values using a v2 deck code format.

### Requirement: Match and Replay Snapshots
The system SHALL store resolved deck snapshots for matches and replays.

#### Scenario: Snapshot on match start
- **WHEN** a match starts
- **THEN** the system SHALL persist the resolved card versions for both decks as a snapshot.

#### Scenario: Replay uses snapshot
- **WHEN** a replay is loaded
- **THEN** the system SHALL use the saved snapshot even if newer card versions exist.

### Requirement: Historical Version Retention
The system SHALL preserve historical versions to keep replays valid.

#### Scenario: Delete referenced version
- **WHEN** a card version is referenced by a replay snapshot
- **THEN** the system SHALL prevent deletion or archive the version so the replay remains valid.
