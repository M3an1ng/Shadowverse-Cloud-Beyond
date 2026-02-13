## ADDED Requirements
### Requirement: Preflight SHALL Progress Abilities As Far As Deterministically Possible
The system SHALL preflight each actionable ability through all deterministically-resolvable steps before commit, including branch expansion from decision effects.

#### Scenario: Ability preflight completes all non-commit steps
- **GIVEN** a player action triggers one or more abilities
- **WHEN** the transaction enters preflight
- **THEN** each ability is advanced through condition check, target resolution, decision resolution, and branch expansion when applicable
- **AND** unresolved work is limited to commit-only state mutation

### Requirement: Cancellation SHALL Be Allowed Until Commit Boundary
The system SHALL allow cancellation for preflight interaction steps and SHALL reject cancellation after commit starts.

#### Scenario: Cancel before commit aborts cleanly
- **GIVEN** a preflight interaction is in progress
- **WHEN** the player cancels
- **THEN** the transaction returns cancelled
- **AND** no irreversible state mutation is committed

#### Scenario: Cancel during commit is rejected
- **GIVEN** commit execution has started
- **WHEN** a cancellation is submitted
- **THEN** the cancellation is rejected
- **AND** commit proceeds according to the preflight plan or fails atomically

### Requirement: Choose Branch Target Selection SHALL Be Prefighted
The system SHALL preflight target selection for abilities created by `Choose/Random` branch expansion, and those selections SHALL be cancellable in preflight.

#### Scenario: Branch ability target is selected in preflight
- **GIVEN** a `Choose` effect expands into branch abilities that require manual target selection
- **WHEN** preflight runs branch expansion
- **THEN** branch target choices are requested and resolved in preflight
- **AND** the player can cancel before commit

### Requirement: Commit SHALL Consume Preflight Plan Without New Interaction
The system SHALL execute only preflight-materialized decisions and target selections during commit and SHALL NOT open new interaction prompts.

#### Scenario: Commit uses snapshots only
- **GIVEN** a completed preflight plan with decision and target snapshots
- **WHEN** commit executes
- **THEN** no new player-choice prompt is emitted
- **AND** effects execute using the preflight snapshots

### Requirement: Commit SHALL Validate Preflight Consistency
The system SHALL validate decision/target signatures and state fingerprint before consuming preflight outputs.

#### Scenario: Fingerprint mismatch blocks commit
- **GIVEN** preflight completed successfully
- **AND** game state fingerprint differs at commit start
- **WHEN** commit validation runs
- **THEN** the transaction fails with a consistency error
- **AND** no partial state mutation is persisted
