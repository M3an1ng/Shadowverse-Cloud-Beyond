## ADDED Requirements

### Requirement: Multiplayer pre-room deck selection
The system SHALL require a player deck to be selected in the existing multiplayer panel before entering matchmaking or a room.

#### Scenario: Missing selection blocks entry
- **WHEN** the player attempts to enter multiplayer without selecting a deck
- **THEN** the UI shows a validation message and does not start matchmaking or join/create a room

#### Scenario: Selected deck is used for matchmaking
- **GIVEN** a deck has been selected in the multiplayer panel
- **WHEN** the player enters matchmaking or a room
- **THEN** the selected deck ID is used for the match flow

### Requirement: Singleplayer panel with separate AI deck selection
The system SHALL provide a singleplayer panel styled to match the multiplayer panel and require separate deck selections for player and AI.

#### Scenario: Separate deck selection required
- **WHEN** the player attempts to start a singleplayer match without selecting both player and AI decks
- **THEN** the UI shows a validation message and does not start the match

#### Scenario: Selected decks are applied
- **GIVEN** both player and AI decks are selected
- **WHEN** the player starts a singleplayer match
- **THEN** the player deck ID and AI deck ID are used in the match flow

### Requirement: No new page for deck selection
The system SHALL implement deck selection within existing UI panels without creating a new page.

#### Scenario: Panel placement
- **WHEN** the user navigates to the existing multiplayer UI
- **THEN** the deck selection controls are present in the multiplayer panel, and the singleplayer panel is available in the same UI surface