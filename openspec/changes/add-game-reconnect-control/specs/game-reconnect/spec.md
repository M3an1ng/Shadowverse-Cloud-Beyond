## ADDED Requirements
### Requirement: Reconnect status and control
The client SHALL display a reconnect status banner and a manual reconnect button when the SignalR connection is closed or reconnecting.

#### Scenario: Connection lost
- **WHEN** the SignalR connection enters a closed or reconnecting state
- **THEN** the UI shows a reconnect status banner and a reconnect button

#### Scenario: Connection restored
- **WHEN** the SignalR connection is re-established
- **THEN** the reconnect UI is hidden and gameplay interactions are re-enabled

### Requirement: Full state resynchronization
The client SHALL request a full game state after reconnect and apply it before resuming gameplay.

#### Scenario: Successful reconnect
- **WHEN** the client reconnects to the game hub
- **THEN** the client requests the latest game state and renders it before enabling input
