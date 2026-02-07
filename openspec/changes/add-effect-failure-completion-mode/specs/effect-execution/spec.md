## ADDED Requirements
### Requirement: Failure completion mode for effects
The system MUST support a failure handling mode that emits EffectCompletedEvent even when an effect fails, marks the failure in Outputs, and allows chained abilities to continue.

#### Scenario: Effect fails but completion is emitted
- **GIVEN** an effect configured with the failure completion mode
- **WHEN** the effect handler returns failure
- **THEN** EffectCompletedEvent is emitted
- **AND** EffectCompletedEvent.Outputs indicates the effect failed

#### Scenario: Chained abilities continue after failed effect
- **GIVEN** an effect configured with the failure completion mode and chained abilities
- **WHEN** the effect handler returns failure
- **THEN** chained abilities execute using EffectCompletedEvent as the triggering event