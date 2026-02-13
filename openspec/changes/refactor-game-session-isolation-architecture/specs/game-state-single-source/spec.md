## ADDED Requirements
### Requirement: Match state MUST have one authoritative mutable source
All runtime components (`Hub`, `GameContextFilter`, `GameService`, background workers) MUST read and write match state through one authoritative state manager interface. A second mutable in-memory state container MUST NOT exist.

#### Scenario: Context filter and command handler read the same state snapshot
- **GIVEN** a match state is updated to version `N`
- **WHEN** `GameContextFilter` resolves context and command handler executes the next command
- **THEN** both components observe the same authoritative state version `N`

### Requirement: Context creation MUST fail-fast on missing authoritative state
If authoritative state for a `gameId` is missing, context creation MUST fail-fast with explicit diagnostics and MUST NOT continue with inferred or stale fallback state.

#### Scenario: Missing match state returns explicit failure
- **GIVEN** a command references a `gameId` that has no authoritative state
- **WHEN** context creation is attempted
- **THEN** the server returns a clear `game context not found` class error
- **AND** the command is not executed

### Requirement: State updates MUST enforce monotonic versioning
Every committed mutation MUST increase the authoritative state version, and writes with stale version preconditions MUST be rejected.

#### Scenario: Stale write is rejected
- **GIVEN** state version is `42`
- **WHEN** a write attempts to commit with precondition version `41`
- **THEN** the write is rejected as stale
- **AND** no partial mutation is persisted

