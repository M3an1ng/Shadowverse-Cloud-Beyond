## ADDED Requirements
### Requirement: Decision Events Persist Outputs
The system SHALL emit a `ChooseEvent` or `RandomEvent` for each Choose or Random effect and persist the final selection outputs on commit.

#### Scenario: Choose decision outputs are committed
- **WHEN** a Choose effect resolves within a transaction
- **THEN** a `ChooseEvent` is emitted
- **AND** the decision event outputs include selected option ids/indexes and counts
- **AND** the outputs reflect the final selection after commit

#### Scenario: Random decision outputs are committed
- **WHEN** a Random effect resolves within a transaction
- **THEN** a `RandomEvent` is emitted
- **AND** the decision event outputs include selected option ids/indexes and counts
- **AND** the outputs reflect the final selection after commit

### Requirement: Branch Abilities Execute In-Transaction
The system SHALL execute decision branch abilities within the same transaction as the parent effect after the decision event is emitted.

#### Scenario: Branch abilities execute after decision event
- **WHEN** a decision effect resolves
- **THEN** its decision event is emitted
- **AND** the selected branch abilities execute within the same transaction before commit completes

### Requirement: Decision Outputs Are Not Stored On EffectCompletedEvent
The system SHALL NOT store decision selection outputs on EffectCompletedEvent; decision outputs SHALL be stored on decision events.

#### Scenario: EffectCompletedEvent omits decision outputs
- **WHEN** a Choose or Random effect completes
- **THEN** EffectCompletedEvent outputs exclude decision selection fields
- **AND** decision selection fields are available on the decision event instead

### Requirement: Choose Cancellation Cancels The Transaction
The system SHALL allow Choose to be cancelled only during the cancellable (Phase 1) window and return a cancelled transaction result without committing state changes.

#### Scenario: Choose is cancelled during Phase 1
- **WHEN** a player cancels a Choose selection during the cancellable window
- **THEN** the transaction returns a cancelled result
- **AND** no commit phase state changes are applied

### Requirement: Commit-Phase Choose Is Non-Cancellable
The system SHALL treat Choose during commit as non-cancellable and require a valid selection.

#### Scenario: Commit-phase Choose cannot be cancelled
- **WHEN** a Choose effect resolves after the transaction has entered commit
- **THEN** cancellation is not accepted
- **AND** the effect proceeds with a valid selection

### Requirement: Cancellable Decisions Are Pre-Resolved In Phase 1
The system SHALL pre-resolve cancellable Choose/Random decisions during Phase 1 and reuse the cached selection during commit.

#### Scenario: Pre-resolve cancellable decision
- **WHEN** a cancellable Choose or Random effect is evaluated during Phase 1
- **THEN** the decision is resolved and cached
- **AND** the commit phase reuses the cached selection without prompting again
