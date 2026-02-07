## ADDED Requirements
### Requirement: Conditional Removal Buff Expiry
The system SHALL remove buffs whose DurationType is ConditionalRemoval once their RemovalCondition evaluates to true.

#### Scenario: Condition becomes true and buff is removed
- **WHEN** a ConditionalRemoval buff has a RemovalCondition that evaluates to true in the current GameContext
- **THEN** the buff is removed and a BuffRemovedEvent is generated

#### Scenario: Condition remains false and buff persists
- **WHEN** a ConditionalRemoval buff has a RemovalCondition that evaluates to false
- **THEN** the buff remains active

#### Scenario: Missing RemovalCondition is rejected
- **WHEN** a ConditionalRemoval buff is created without a RemovalCondition
- **THEN** the buff application fails with a validation error