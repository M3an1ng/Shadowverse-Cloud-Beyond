## ADDED Requirements
### Requirement: EffectCompletedEvent exposes generated cards
The system SHALL expose a GeneratedCards parameter on EffectCompletedEvent that includes newly created card instances during the effect execution and excludes pre-existing instances.

#### Scenario: Include new cards only
- **WHEN** an effect creates new card instances during its handler execution
- **THEN** GeneratedCards contains the newly created CardInstance objects and excludes pre-existing cards

#### Scenario: No new cards
- **WHEN** an effect does not create any new card instances
- **THEN** GeneratedCards is an empty list