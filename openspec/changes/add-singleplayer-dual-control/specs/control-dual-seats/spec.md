## ADDED Requirements
### Requirement: Dual-Control Match Setup
The system SHALL allow a single client to start a dual-control match with two human-controlled seats.

#### Scenario: Create a dual-control room
- **WHEN** the player starts a singleplayer match in dual-control mode
- **THEN** the system creates a room with host and guest seats and obtains credentials for both seats.

#### Scenario: Deck selection mirrors VS-AI
- **WHEN** the player selects a primary deck and an opponent deck in dual-control mode
- **THEN** the system submits the primary deck for the host seat and the opponent deck for the guest seat before starting the match.

### Requirement: Dual-Control Action Routing
The client SHALL route actions using the credentials of the currently controlled seat.

#### Scenario: Active seat takes an action
- **WHEN** the controlled seat is the current turn player and the player performs an action
- **THEN** the client sends the action using that seat's credentials and the server processes it as that seat.

### Requirement: Dual-Control Seat Switching
The client SHALL allow switching the controlled seat during a dual-control match.

#### Scenario: Turn change updates control
- **WHEN** the turn changes to the other seat
- **THEN** the client updates the controlled seat (and input gating) to match the new current player.

### Requirement: Dual-Control Mulligan and Choices
The client SHALL support mulligan and choice prompts for both seats in dual-control mode.

#### Scenario: Mulligan for each seat
- **WHEN** the match enters the mulligan phase
- **THEN** the client allows submitting mulligan decisions for each seat using the corresponding credentials.

### Requirement: Dual-Control Disables AI Automation
The system SHALL NOT auto-execute AI actions in dual-control matches.

#### Scenario: No AI turn execution
- **WHEN** a dual-control match is running
- **THEN** no AI mulligan or AI turn executor is triggered for either seat.
