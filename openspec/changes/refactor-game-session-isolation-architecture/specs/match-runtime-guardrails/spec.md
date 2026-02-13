## ADDED Requirements
### Requirement: Match runtime logs MUST be structured and semantically correct
Critical command and event logs MUST include correct `gameId`, `playerId`, `cardUid/entityId`, `commandId`, and sequence/version fields without placeholder misbinding.

#### Scenario: PlayCard success log uses correct identifiers
- **GIVEN** a `PlayCard` command succeeds
- **WHEN** the success log is written
- **THEN** the `gameId` field contains the match identifier
- **AND** the `cardUid` field contains the card instance identifier

### Requirement: Cross-game entity drift MUST trigger runtime alerting
The runtime MUST detect and alert when the same `cardUid` or `entityId` appears under multiple active `gameId` values within a configured time window.

#### Scenario: Same cardUid appears in two active matches
- **GIVEN** telemetry receives events mapping one `cardUid` to `gameId=A` and `gameId=B`
- **WHEN** both matches are active within the alert window
- **THEN** the system emits a high-severity alert with both correlation chains

### Requirement: Migration rollout MUST support staged flags and automatic rollback
Architecture migration controls MUST be feature-flagged by phase and MUST support automatic rollback when SLO guardrails are violated.

#### Scenario: Guardrail breach triggers rollback
- **GIVEN** Phase 2 single-writer path is enabled for a canary cohort
- **WHEN** guardrail metrics exceed threshold for the configured duration
- **THEN** the runtime automatically disables Phase 2 flag for that cohort
- **AND** traffic falls back to the previous stable phase

