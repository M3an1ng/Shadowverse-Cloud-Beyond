## ADDED Requirements
### Requirement: Refreshable Persistent Auras
The system SHALL allow the DSL to declare a persistent aura that registers a target set, applies a defined list of effects as buffs, and remains active until removed by duration or source removal.

#### Scenario: Aura registration applies to current targets
- **WHEN** an ability with an aura definition is executed
- **THEN** the aura is registered and its effects are applied as buffs to all current targets that match the aura selection

### Requirement: Aura Refresh on Triggered Events
The system SHALL refresh aura target sets when an event matches the aura refresh triggers and conditions, applying effects to newly eligible targets and removing effects from targets that no longer match.

#### Scenario: Targets change after transform
- **WHEN** a card transforms into a type that matches the aura target filter
- **THEN** the aura refresh applies its effects to the transformed card

#### Scenario: Targets leave the aura scope
- **WHEN** a card leaves the aura target scope due to zone change
- **THEN** the aura refresh removes its effects from that card

### Requirement: Deterministic Buff Identity and Loop Guards
The system SHALL apply aura effects using deterministic buff identity and prevent refresh loops caused by aura-generated events.

#### Scenario: Aura refresh is idempotent
- **WHEN** the same refresh event is processed more than once for an aura
- **THEN** the system does not stack duplicate buffs for the same aura and target