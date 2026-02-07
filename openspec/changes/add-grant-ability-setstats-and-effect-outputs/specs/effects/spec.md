## ADDED Requirements
### Requirement: GrantAbility effect
The system MUST provide a GrantAbility effect that attaches ability configurations to target cards through the Buff system and respects duration.

#### Scenario: Granted ability triggers while buff is active
- **WHEN** a GrantAbility effect applies a triggered ability to a follower until end of turn
- **THEN** the follower can trigger that ability while the buff is active
- **AND** the granted ability is removed when the duration expires

### Requirement: SetStats effect
The system MUST provide a SetStats effect that sets attack/health/cost using StatChangedEvent with set-layer modifiers.

#### Scenario: SetStats overrides base stats
- **WHEN** a SetStats effect sets a follower's attack and health to fixed values
- **THEN** the resulting stats use set-layer modifiers and do not stack additively with the previous values

### Requirement: EffectCompletedEvent exposes generated cards for chaining
The system MUST expose cards created by an effect on EffectCompletedEvent so chained abilities can target them.

#### Scenario: Chained ability targets generated card
- **WHEN** a Summon effect generates a card and a chained ability targets context.triggeringEvent.generatedCards
- **THEN** the chained ability can select the summoned card as a valid target
