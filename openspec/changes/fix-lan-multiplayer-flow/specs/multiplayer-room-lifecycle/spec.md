## ADDED Requirements
### Requirement: Leaving Multiplayer MUST Close Room
When a player explicitly leaves a multiplayer room (e.g., returns to main menu), the client MUST call a leave endpoint and the server MUST close the room so it cannot be reused or joined.

#### Scenario: Leave lobby closes room
- **GIVEN** a PVP room exists and the user is in the multiplayer lobby
- **WHEN** the user leaves the lobby (back to main menu)
- **THEN** the client MUST call `POST /api/match/{id}/leave` with `{ playerId, token }`
- **AND** the room MUST transition to `Closed` with a `closedReason`

#### Scenario: Leaving lobby must not affect lobby->game transition
- **GIVEN** a PVP room is `Started`
- **WHEN** the user enters gameplay from the lobby
- **THEN** the client MUST NOT call `leave` during the lobby cleanup transition

### Requirement: Opponent MUST Be Notified On Close
When a room is closed due to leave, the opponent MUST be notified.

#### Scenario: Opponent in lobby sees Closed
- **GIVEN** the opponent is still in the multiplayer lobby and polling room status
- **WHEN** the room is closed due to leave
- **THEN** the polling response MUST show `status=Closed` and `closedReason`

#### Scenario: Opponent in game receives ROOM_CLOSED
- **GIVEN** the opponent is connected to gameplay via SignalR
- **WHEN** the room is closed due to leave
- **THEN** the server MUST send `ReceiveError("ROOM_CLOSED:<reason>")` to the game group
- **AND** the client MUST return to the main menu after showing the reason
