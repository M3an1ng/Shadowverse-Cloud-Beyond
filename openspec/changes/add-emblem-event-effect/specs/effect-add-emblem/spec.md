## ADDED Requirements
### Requirement: AddEmblem Event and Effect
The system SHALL provide an AddEmblem effect that emits an AddEmblem event to place cards into the Emblem zone.

#### Scenario: Definition mode adds emblem cards
- **WHEN** AddEmblem runs with CardSourceMode = Definition and a valid CardDefinitionId
- **THEN** the system creates new card instances and commits them into the target player Emblem zone

#### Scenario: Instance mode moves selected cards
- **WHEN** AddEmblem runs with CardSourceMode = Instance and selected card targets
- **THEN** the system moves the selected card instances into their owners' Emblem zones

### Requirement: Crest-only Validation
Only crest/emblem cards SHALL be allowed to enter the Emblem zone through AddEmblem.

#### Scenario: Reject non-crest cards
- **WHEN** AddEmblem targets a card definition or instance whose CardType is not Crest
- **THEN** the effect fails validation and emits no AddEmblem events

### Requirement: Duplicate Emblem Guard
AddEmblem SHALL support a duplicate-check toggle to prevent adding emblems with the same base definition ID.

#### Scenario: Duplicate check skips existing emblems
- **WHEN** duplicate-check is enabled and the target player's Emblem zone already contains the same base definition ID
- **THEN** no new emblem is added for that definition
