## ADDED Requirements
### Requirement: Navigation-time match return decision
The client SHALL ask the player whether to return to the ongoing multiplayer match or disconnect after entering the main navigation page with an active non-AI room context.

#### Scenario: Prompt is shown after leaving match-related routes
- **WHEN** the player navigates from `game` or `multiplayer` to `navigation`
- **AND** the client still has a valid non-AI room handshake
- **THEN** the client shows a decision prompt with "Return to Match" and "Disconnect"

#### Scenario: Return to match does not leave room
- **WHEN** the player selects "Return to Match"
- **THEN** the client navigates back to `game`
- **AND** the client MUST NOT call the room leave endpoint
- **AND** the cached handshake remains available

#### Scenario: Disconnect explicitly leaves room
- **WHEN** the player selects "Disconnect"
- **THEN** the client calls the room leave endpoint with the current handshake credentials
- **AND** the cached handshake is cleared
- **AND** reconnect/return actions for that room are disabled

#### Scenario: Server-closed room clears pending decision
- **WHEN** the room is closed by server signal before the player confirms
- **THEN** the client clears pending decision state
- **AND** the client does not show a stale return-to-match prompt
