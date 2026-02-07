## MODIFIED Requirements
### Requirement: AuraConfig Produces Buffs
The system SHALL treat AuraConfig as a persistent ability template that produces and manages Buff instances via the AbilityManager execution pipeline and AuraManager (or AuraExecutor).

#### Scenario: AuraConfig registers buffs
- **WHEN** an ability with AuraConfig is activated or registered
- **THEN** the AuraConfig is registered for refresh and materializes Buff instances directly
- **AND** any effects are applied via Buff add/remove events

#### Scenario: Aura refresh runs through ability pipeline
- **WHEN** an Aura refresh trigger event occurs
- **THEN** the AbilityManager pipeline evaluates conditions and resolves targets
- **AND** the Aura execution branch refreshes Buffs for the resolved targets

## ADDED Requirements
### Requirement: Aura Execution Branch
The system SHALL execute Aura abilities via a dedicated Aura/Buff refresh branch instead of creating EffectQueue entries.

#### Scenario: Aura execution does not enqueue effects
- **WHEN** an Aura ability executes
- **THEN** no EffectQueue entries are created
- **AND** Buff add/remove events are generated based on target diffs
