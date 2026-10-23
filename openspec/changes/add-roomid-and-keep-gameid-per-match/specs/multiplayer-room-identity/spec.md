## ADDED Requirements

### Requirement: Room and Game identifiers MUST be separated
Multiplayer room lifecycle MUST be identified by `roomId`, and single-match lifecycle MUST be identified by `gameId`.

#### Scenario: Create room returns room identifier
- **WHEN** host creates a multiplayer room
- **THEN** system returns a stable `roomId` for room lifecycle operations
- **AND** system does not treat that `roomId` as a single-match `gameId`

#### Scenario: Start match creates match identifier
- **WHEN** a room transitions from ready to started
- **THEN** system generates a new `gameId` for the started match
- **AND** that `gameId` is stored as current active match for the room

### Requirement: Room-scoped and game-scoped APIs MUST use correct identifier
Room management APIs MUST use `roomId`; replay, settlement, and stats APIs MUST continue using `gameId`.

#### Scenario: Room operation uses roomId
- **WHEN** client calls join/ready/start/leave endpoints
- **THEN** backend resolves room by `roomId`
- **AND** returns current active `gameId` after match start

#### Scenario: Replay and settlement keep gameId semantics
- **WHEN** client requests replay data or backend persists settlement/stats
- **THEN** system reads/writes by single-match `gameId`
- **AND** existing uniqueness/idempotency constraints remain valid

### Requirement: System MUST provide game-to-room reverse lookup
Backend MUST support reverse lookup from `gameId` to `roomId` for authorization and lifecycle checks.

#### Scenario: Replay access check needs room context
- **WHEN** replay access requires join-code validation
- **THEN** backend resolves `roomId` from `gameId`
- **AND** validates join-code against the resolved room

#### Scenario: Runtime room check from game loop
- **WHEN** game runtime needs to verify room status (e.g., timeout, disconnect handling)
- **THEN** backend resolves `roomId` from `gameId`
- **AND** applies room-closure/expiry logic on the resolved room
