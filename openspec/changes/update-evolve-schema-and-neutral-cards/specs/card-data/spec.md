## ADDED Requirements

### Requirement: Remove EvolveId From Card Definitions
Card definitions SHALL NOT include evolve_id, and evolution MUST NOT rely on definition swapping.

#### Scenario: Card data load without evolve_id
- **WHEN** card definitions are loaded from JSON or database
- **THEN** cards without evolve_id remain valid and evolvable only via fixed evolution rules

### Requirement: Detective Magnifying Glass Uses Act
The Detective's Magnifying Glass card SHALL expose its activation as an Act ability triggered by EventType.Act, not CountdownFinished.

#### Scenario: Player activates Detective's Magnifying Glass
- **WHEN** the owning player uses Act on Detective's Magnifying Glass
- **THEN** the configured Act effects resolve
