## ADDED Requirements
### Requirement: PVP Seat Mapping Uses Handshake PlayerId
In a PVP match (multiple non-AI players), the client MUST treat the lobby handshake `playerId` as `localPlayerId` and MUST NOT infer/overwrite it during full-state hydration.

#### Scenario: Two humans render distinct hands
- **GIVEN** host and guest connect with different `playerId` values
- **WHEN** both clients receive the same full `GameState` containing both players
- **THEN** each client MUST render the hand/mulligan state for its own `localPlayerId` and MUST render the other player as opponent

#### Scenario: localPlayerId mismatch is not inferred
- **GIVEN** the client has a `localPlayerId` that does not exist in `GameState.Players`
- **WHEN** the UI renders a full game state
- **THEN** the client MUST log an error and MUST NOT infer a seat from `IsAI` or array ordering

### Requirement: VS-AI Seat Inference Is Allowed
When a full `GameState` contains exactly one non-AI player and one AI player, the client MUST infer `localPlayerId` as the non-AI player if no handshake is available.

#### Scenario: VS-AI fallback without handshake
- **GIVEN** the client has no cached lobby handshake
- **AND** the received `GameState.Players` contains exactly one non-AI player and one AI player
- **WHEN** the client hydrates a full game state
- **THEN** the client MUST select the non-AI player as `localPlayerId`
