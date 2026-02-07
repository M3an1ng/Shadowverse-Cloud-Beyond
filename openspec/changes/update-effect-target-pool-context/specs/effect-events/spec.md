## MODIFIED Requirements

### Requirement: Effect completion event payload
The system SHALL include the generated event list in EffectCompletedEvent and SHALL NOT include GeneratedCards.

#### Scenario: EffectCompletedEvent captures generated events
- **WHEN** an effect executes and produces events
- **THEN** EffectCompletedEvent exposes the generated event list
- **AND** GeneratedCards is not present on the event payload