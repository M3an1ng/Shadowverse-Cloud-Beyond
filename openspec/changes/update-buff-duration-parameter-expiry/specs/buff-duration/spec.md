## ADDED Requirements
### Requirement: Parameterized End/Start-of-Turn Buff Expiry
The system SHALL interpret DurationParameter as a positive integer N for buffs whose DurationType is EndOfTurn or StartOfTurn. The buff MUST expire and be removed on the Nth matching phase after it is applied.

#### Scenario: EndOfTurn expires on Nth end of turn
- **WHEN** a buff with DurationType EndOfTurn and DurationParameter = 2 is applied during player A's turn
- **THEN** the buff remains active through that turn end and is removed at the end of the following turn

#### Scenario: StartOfTurn expires on Nth start of any turn
- **WHEN** a buff with DurationType StartOfTurn and DurationParameter = 2 is applied during player A's turn
- **THEN** the buff is removed at the start of the second subsequent turn (global)

#### Scenario: Missing or non-positive parameter defaults to 1
- **WHEN** a buff with DurationType EndOfTurn or StartOfTurn has DurationParameter missing or <= 0
- **THEN** the buff expires on the first matching phase
