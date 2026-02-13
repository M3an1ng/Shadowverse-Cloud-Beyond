## ADDED Requirements
### Requirement: Per-game command execution MUST be single-writer
The runtime MUST process state-mutating commands for the same `gameId` through exactly one serialized execution path. Concurrent writes within the same match MUST NOT execute in parallel.

#### Scenario: Concurrent commands are serialized in one match
- **GIVEN** two players submit `PlayCard` and `EndTurn` at nearly the same time for the same `gameId`
- **WHEN** the server accepts both requests
- **THEN** the runtime executes them in a deterministic queue order for that `gameId`
- **AND** state version increments monotonically without interleaving writes

### Requirement: Client retries MUST be idempotent with command identity
The runtime MUST support idempotent command retries using `clientCommandId` scoped by `gameId` and `playerId`, so duplicate submissions do not apply effects twice.

#### Scenario: Duplicate retry does not re-apply effect
- **GIVEN** a command has already been committed for (`gameId`, `playerId`, `clientCommandId`)
- **WHEN** the same command is retried due to network reconnect
- **THEN** the server returns the prior command result
- **AND** no additional game effect is committed

### Requirement: Cross-game command routing MUST be isolated
The command router MUST route each mutation request strictly by `gameId` and MUST NOT reuse execution context from another match.

#### Scenario: Command cannot enter a different match session
- **GIVEN** a request carries `gameId=A`
- **WHEN** the runtime dispatches the request
- **THEN** the request can only execute inside session `A`
- **AND** any missing session for `A` fails with a diagnosable error instead of falling back to another match

