## ADDED Requirements
### Requirement: Trigger count limits
The system SHALL allow a trigger to define optional maximum trigger counts per turn and per match, scoped to the ability instance that owns the trigger.

#### Scenario: No limits configured
- **WHEN** no per-turn or per-match limit is specified
- **THEN** the trigger is not restricted by count-based limits

#### Scenario: Per-turn limit reached
- **WHEN** a per-turn limit is specified and the current turn count is greater than or equal to the limit
- **THEN** the trigger SHALL NOT fire for subsequent events during that turn

#### Scenario: Per-match limit reached
- **WHEN** a per-match limit is specified and the total trigger count is greater than or equal to the limit
- **THEN** the trigger SHALL NOT fire for subsequent events for the remainder of the match

#### Scenario: Unlimited limit value
- **WHEN** a per-turn or per-match limit is set to 0 or a negative value
- **THEN** the trigger is not restricted by that limit

#### Scenario: Both limits configured
- **WHEN** both per-turn and per-match limits are specified
- **THEN** the trigger SHALL fire only while both limits are not exceeded
