## ADDED Requirements
### Requirement: DSL can access CardInstance parameters from TriggeringEvent
The DSL MUST provide `eventCard(name)` to resolve CardInstance parameters from TriggeringEvent. When the parameter is missing or not a CardInstance, it MUST return null.

#### Scenario: Summon event exposes the summoned card
- **WHEN** a TriggeringEvent includes a CardInstance parameter
- **THEN** `eventCard` returns that CardInstance

#### Scenario: Missing CardInstance returns null
- **WHEN** the TriggeringEvent does not include the requested CardInstance parameter
- **THEN** `eventCard` returns null
