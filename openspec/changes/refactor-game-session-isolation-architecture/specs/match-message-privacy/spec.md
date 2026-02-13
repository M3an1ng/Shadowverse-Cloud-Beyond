## ADDED Requirements
### Requirement: Match event fanout MUST enforce visibility tiers
The server MUST classify each outbound event as `Public` or `Private` before fanout. `Public` events MAY be sent to `Game_<gameId>`; `Private` events MUST be sent only to `Player_<playerId>_<gameId>`.

#### Scenario: Hand playability is private
- **GIVEN** playability recalculation finishes for player `P1` in match `G`
- **WHEN** outbound events are emitted
- **THEN** hand playability details are sent only to `Player_<P1>_<G>`
- **AND** no equivalent payload is sent to `Game_<G>`

### Requirement: Public payloads MUST exclude private fields
For events routed to `Game_<gameId>`, payload schemas MUST exclude private card identity and private decision metadata.

#### Scenario: Public game update redacts hidden hand details
- **GIVEN** a game update includes both shared board state and hidden hand metadata
- **WHEN** emitting to `Game_<gameId>`
- **THEN** hidden hand metadata is removed or redacted from the public payload

### Requirement: Visibility policy violations MUST be auditable
If a private field is detected in a public payload, the runtime MUST record an audit event with `gameId`, `eventType`, `fieldPath`, and correlation identifiers.

#### Scenario: Policy violation emits audit record
- **GIVEN** an outbound payload marked `Public` still contains a private field
- **WHEN** visibility lint checks run before send
- **THEN** the send is blocked or downgraded per policy
- **AND** an audit log entry is emitted for incident investigation

