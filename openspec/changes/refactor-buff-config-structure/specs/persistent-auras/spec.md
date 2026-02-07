## ADDED Requirements
### Requirement: AuraConfig Stores BuffConfig List
The system SHALL allow AuraConfig to store a list of BuffConfig entries that describe individual Buff generation rules.

#### Scenario: AuraConfig lists BuffConfig entries
- **WHEN** an AuraConfig is loaded
- **THEN** it contains a BuffConfig list rather than a single BuffTemplateConfig

### Requirement: Aura Applies BuffConfigs Without Merge
The system SHALL apply each BuffConfig as an independent Buff instance during aura refresh without merging them into a single composite Buff.

#### Scenario: BuffConfigs applied independently
- **WHEN** an aura refresh occurs with multiple BuffConfig entries
- **THEN** the system generates one Buff per BuffConfig
- **AND** the EffectKey follows the standard buff rule derived from SourceAbilityId and EffectIndex