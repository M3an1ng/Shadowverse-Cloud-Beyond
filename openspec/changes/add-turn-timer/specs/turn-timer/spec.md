## ADDED Requirements
### Requirement: Turn timeout enforcement
The system SHALL start a per-turn timer at TurnStart and enforce a 180-second turn limit.

#### Scenario: Start timer at TurnStart
- **WHEN** a TurnStart event is committed for the active player
- **THEN** the server starts a 180-second turn timer for that player

#### Scenario: Stop timer on turn end
- **WHEN** a player's turn ends normally
- **THEN** the server stops the running timer for that player

#### Scenario: Timeout ends the turn
- **WHEN** the turn timer reaches zero and the active player is still current
- **THEN** the system auto-ends the turn as if EndTurn was requested

### Requirement: Timeout resolves pending choices
The system SHALL resolve any pending player choice on timeout before ending the turn.

#### Scenario: Cancellable choice is cancelled on timeout
- **WHEN** the turn timer expires and the pending choice is cancellable
- **THEN** the system resolves the choice as cancelled and ends the turn

#### Scenario: Non-cancellable choice is force-resolved on timeout
- **WHEN** the turn timer expires and the pending choice is not cancellable
- **THEN** the system selects random legal targets that satisfy the minimum selection count and submits the choice before ending the turn

### Requirement: Timing metadata is sent to clients
The system SHALL send turn timing metadata to clients so they can render a countdown.

#### Scenario: TurnStarted event includes timing data
- **WHEN** a TurnStarted event is broadcast
- **THEN** the payload includes turn duration seconds and the absolute turn end time

### Requirement: End-turn button shows remaining time
The client SHALL display the remaining turn time as a number and a progress bar on the end-turn button during the local player's turn.

#### Scenario: Countdown display during local turn
- **WHEN** the local player's turn is active
- **THEN** the end-turn button shows a numeric countdown and a progress bar that depletes to zero

#### Scenario: Countdown hidden when not local turn
- **WHEN** it is not the local player's turn
- **THEN** the end-turn button timer display is hidden or inactive
