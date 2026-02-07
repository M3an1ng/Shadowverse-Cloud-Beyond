## ADDED Requirements
### Requirement: AuraConfig Produces Buffs
The system SHALL treat AuraConfig as a persistent ability template that produces and manages Buff instances via AuraManager.

#### Scenario: AuraConfig registers buffs
- **WHEN** an ability with AuraConfig is activated or registered
- **THEN** the AuraConfig is registered in AuraManager and materializes Buff instances directly
- **AND** any effects are applied via Buff add/remove events

### Requirement: AuraConfig Default Inputs
The system SHALL allow AuraConfig to inherit Ability-level Targets/Conditions/Properties by default.

#### Scenario: AuraConfig uses Ability defaults
- **WHEN** AuraConfig omits targets or conditions
- **THEN** the Ability's Targets/Conditions/Properties are used for aura refresh and buff application

### Requirement: AuraConfig Overrides
The system SHALL allow AuraConfig to override Ability-level Targets/Conditions/Properties when specified.

#### Scenario: AuraConfig overrides targets
- **WHEN** AuraConfig defines override targets
- **THEN** the aura refresh uses the override targets instead of the Ability defaults

### Requirement: Aura Effect Safety
The system SHALL restrict AuraConfig Buff templates to refresh-safe effects (e.g., set/modify status, keywords, grant ability).

#### Scenario: Unsupported effect rejected
- **WHEN** AuraConfig includes an unsupported effect type
- **OR** includes an unsupported Buff template field
- **THEN** registration fails with an error

### Requirement: Aura Conditions Gate Overall Enablement
The system SHALL treat AuraConfig conditions as the overall on/off gate for the aura.

#### Scenario: Aura disabled by condition
- **WHEN** AuraConfig conditions evaluate to false
- **THEN** the aura does not apply buffs and any existing aura buffs are removed
