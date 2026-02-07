## ADDED Requirements
### Requirement: Emblem Zone Display
The system SHALL display a five-slot emblem zone in player stats between the stats header and resources grid.

#### Scenario: Empty emblem zone
- **WHEN** a player has no emblems
- **THEN** the emblem zone shows five empty slots in a 3-top/2-bottom layout

#### Scenario: Emblem previews
- **WHEN** a player has emblems in the zone
- **THEN** each emblem slot shows the card preview image for that emblem

### Requirement: Emblem Uniqueness and Capacity
The system SHALL prevent multiple emblems with the same name in a single player emblem zone and SHALL discard any emblem that would exceed the limit of five.

#### Scenario: Duplicate emblem
- **WHEN** AddEmblemEffect targets a player who already has an emblem with the same name
- **THEN** the new emblem is not added to the emblem zone

#### Scenario: Emblem zone overflow
- **WHEN** AddEmblemEffect would add a sixth emblem for a player
- **THEN** the new emblem is discarded using the same overflow handling as battlefield limits