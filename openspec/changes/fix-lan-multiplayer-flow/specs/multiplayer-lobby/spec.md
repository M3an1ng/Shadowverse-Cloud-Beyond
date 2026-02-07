## ADDED Requirements
### Requirement: Multiplayer Lobby Join Flow
The client MUST only enter the match gameplay once a PVP room is started.

#### Scenario: Host creates and waits
- **GIVEN** the host created a PVP room and is ready
- **WHEN** the guest has not joined yet
- **THEN** the UI MUST keep the host in the multiplayer lobby and MUST NOT initialize the gameplay connection

#### Scenario: Guest joins and match starts
- **GIVEN** a PVP room exists with a host ready
- **WHEN** the guest joins and becomes ready
- **THEN** the match MUST transition to `Started` and both clients MAY enter gameplay and establish SignalR connection

### Requirement: Single-Player Must Not Reuse PVP Handshake
When entering single-player, the client MUST NOT reuse a cached PVP lobby handshake.

#### Scenario: Cached PVP handshake exists
- **GIVEN** local storage contains a handshake for a PVP room (`useAiFallback=false`)
- **WHEN** the user enters single-player from the main menu
- **THEN** the client MUST create a new AI match handshake and connect using that new handshake
