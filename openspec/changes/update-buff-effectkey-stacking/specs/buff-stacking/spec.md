## ADDED Requirements
### Requirement: Buffs derive EffectKey from their source ability
The system SHALL assign an EffectKey to every buff created by an ability effect using the format `sourceAbilityId:effectIndex`, independent of CanStack. Buffs created without a source ability SHALL leave EffectKey unset and be treated as system-generated buffs.

#### Scenario: Ability effect creates a buff
- **WHEN** an ability effect creates a buff for a target
- **THEN** the buff SHALL have EffectKey in the format `sourceAbilityId:effectIndex`

#### Scenario: System buff creation
- **WHEN** a buff is created without a source ability
- **THEN** the buff SHALL not set EffectKey and SHALL be treated as a system buff

## MODIFIED Requirements
### Requirement: Non-stackable buffs are scoped to effects
Buff stacking SHALL be evaluated during effect handling. Effects that add buffs SHALL expose CanStack; when CanStack is false and a target already has a buff with the same EffectKey, the effect SHALL not apply and SHALL NOT replace existing buffs. When CanStack is true, multiple buffs with the same EffectKey MAY coexist. If EffectKey is missing, the effect SHALL be treated as stackable.

#### Scenario: Same effect re-applied with CanStack false
- **WHEN** an effect with CanStack set to false applies a buff and the target already has a buff with the same EffectKey
- **THEN** the effect SHALL add no buff and SHALL leave the existing buff unchanged

#### Scenario: Same effect re-applied with CanStack true
- **WHEN** an effect with CanStack set to true applies a buff and the target already has a buff with the same EffectKey
- **THEN** the new buff SHALL be added and coexist with the existing buff

#### Scenario: Missing EffectKey
- **WHEN** an effect applies a buff without an EffectKey
- **THEN** the effect SHALL be treated as stackable and apply normally
