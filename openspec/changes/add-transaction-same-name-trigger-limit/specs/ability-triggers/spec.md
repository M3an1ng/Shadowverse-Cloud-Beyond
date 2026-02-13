## ADDED Requirements
### Requirement: Same-name trigger limit per transaction
The system SHALL support an optional trigger configuration that limits same-name abilities to trigger at most once within a single transaction.

#### Scenario: Same-name identity definition
- **WHEN** same-name transaction limiting is enabled
- **THEN** same-name identity SHALL be evaluated by `(owner player, ability definition id)` unless an explicit future policy overrides it

#### Scenario: Option disabled
- **WHEN** the same-name transaction limit option is not configured or is set to false
- **THEN** trigger behavior SHALL remain unchanged from existing logic

#### Scenario: Same-name abilities compete in one transaction
- **WHEN** multiple trigger-eligible abilities with the same name identity are collected in the same transaction and the option is enabled
- **THEN** at most one of those abilities SHALL be allowed to trigger in that transaction

#### Scenario: Winner selection respects existing trigger order
- **WHEN** multiple same-name candidates are eligible at the same timing point in a transaction and the option is enabled
- **THEN** the first candidate under the existing trigger ordering policy SHALL be selected, and the rest SHALL be suppressed for that transaction

#### Scenario: Different name identities remain independent
- **WHEN** trigger-eligible abilities have different name identities in the same transaction
- **THEN** they SHALL NOT suppress each other under this option

#### Scenario: Owner scope isolation
- **WHEN** two players each have abilities with the same name identity and the option is enabled
- **THEN** each player's same-name limit SHALL be evaluated independently in the same transaction

#### Scenario: Transaction boundary reset
- **WHEN** a new transaction starts
- **THEN** the same-name trigger limit state from the previous transaction SHALL NOT carry over
