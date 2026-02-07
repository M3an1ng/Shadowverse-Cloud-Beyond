## ADDED Requirements
### Requirement: Duration Settings for Buff-Producing Effects
The system SHALL expose DurationType and DurationParameter on effects that create buffs (including ModifyStatsEffect, SetKeywordEffect, and GrantAbilityEffect), and pass those values through to buff creation.

#### Scenario: EndOfTurn uses duration parameter
- **WHEN** an effect creates a buff with DurationType EndOfTurn and DurationParameter = 2
- **THEN** the buff expires on the second end-of-turn phase

#### Scenario: StartOfTurn uses duration parameter
- **WHEN** an effect creates a buff with DurationType StartOfTurn and DurationParameter = 2
- **THEN** the buff expires on the second start-of-turn phase