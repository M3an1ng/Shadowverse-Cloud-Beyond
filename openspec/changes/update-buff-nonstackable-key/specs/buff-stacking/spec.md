## ADDED Requirements
### Requirement: Non-stackable buffs are scoped to effects
When a buff is marked as non-stackable, the system SHALL only replace existing buffs created by the same ability effect key.

#### Scenario: Same ability, different effects
- **WHEN** an ability has multiple effects that each add a non-stackable buff
- **THEN** buffs created by different effects SHALL NOT replace each other

#### Scenario: Same ability, same effect re-applied
- **WHEN** the same effect re-applies a non-stackable buff
- **THEN** the newly applied buff SHALL replace the existing buff from that effect

#### Scenario: Missing key
- **WHEN** a non-stackable key is not set on a buff
- **THEN** the buff SHALL be treated as stackable
