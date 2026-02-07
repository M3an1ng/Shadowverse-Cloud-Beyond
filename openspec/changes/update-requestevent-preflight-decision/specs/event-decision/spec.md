## ADDED Requirements
### Requirement: RequestEvent SHALL Be Expanded To ActionRoot Before Primary Decision
The system SHALL derive `ActionRoot` events from incoming `RequestEvent` commands before primary ability preflight decision processing.

#### Scenario: RequestEvent expands to action root first
- **GIVEN** a player submits a request command event
- **WHEN** the transaction starts
- **THEN** the request is expanded into one or more `ActionRoot` events
- **AND** primary ability preflight decision logic runs against `ActionRoot` events

### Requirement: Preflight Decision Uses Rewritten Parameters
The system SHALL resolve RequestEvent-driven Choose/Random decisions in a preflight stage that applies before-intercepts prior to presenting options.

#### Scenario: Rewritten selection count is visible before confirmation
- **GIVEN** a RequestEvent triggers a Choose effect with `selectionCount = 1`
- **AND** a before-intercept rewrites `SelectionCount` to `2`
- **WHEN** the player enters the preflight decision stage
- **THEN** the presented decision requires selecting `2` options
- **AND** the preflight selection payload uses the rewritten event parameters

### Requirement: Preflight Decision Supports Cancellation
The system SHALL allow cancelling Choose decisions only during the preflight stage for cancellable player actions.

#### Scenario: Cancel in preflight rolls back the action request
- **GIVEN** a player action is marked cancellable
- **AND** its RequestEvent enters preflight decision
- **WHEN** the player cancels the Choose prompt
- **THEN** the transaction returns a cancelled result
- **AND** no irreversible state change from the RequestEvent is committed

### Requirement: Commit Reuses Preflight Decisions Without Cancellation
The system SHALL reuse validated preflight decision results during commit and SHALL reject cancellation in commit-phase decision resolution.

#### Scenario: Commit consumes cached preflight choose result
- **GIVEN** a RequestEvent has completed preflight Choose selection
- **WHEN** the transaction enters commit
- **THEN** the corresponding ChooseEvent output is backfilled from cached decision results
- **AND** branch abilities execute in the same transaction
- **AND** commit-phase cancellation is not allowed

### Requirement: Commit SHALL Validate Decision Input Signature
The system SHALL validate that commit-time decision input matches the preflight decision input signature before reusing cached selections.

#### Scenario: Signature mismatch aborts transaction
- **GIVEN** a preflight decision cache exists for an action
- **AND** commit-time option set or selection constraints differ from cached input signature
- **WHEN** commit attempts to reuse the cached selection
- **THEN** the transaction fails with a diagnostic signature mismatch error
- **AND** no partial branch execution is committed

### Requirement: RequestEvent Preflight SHALL Avoid Irreversible State Mutation
The system SHALL avoid irreversible state mutation during RequestEvent preflight and apply such mutations only in committable event commit stage.

#### Scenario: Attack request canceled before commit keeps attack counters unchanged
- **GIVEN** an attack request that requires preflight decision
- **WHEN** the player cancels during preflight
- **THEN** attacker attack counters and related action rights remain unchanged
- **AND** no irreversible resource deduction is persisted
