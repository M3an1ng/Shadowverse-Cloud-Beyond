## ADDED Requirements
### Requirement: Deterministic same-zone trigger ordering
The system SHALL resolve triggered abilities from cards in the same zone according to zone position order (`ZoneIndex`, ascending).

#### Scenario: Field triggers resolve by position
- **WHEN** three followers in the same player's field trigger on the same event
- **THEN** their abilities resolve in ascending `ZoneIndex` order

#### Scenario: Inserted card updates trigger order
- **WHEN** a follower is inserted into the middle of a field and a shared trigger event occurs
- **THEN** trigger resolution reflects the updated contiguous `ZoneIndex` order

### Requirement: Stable tie-breakers for equal position
The system SHALL apply deterministic tie-breakers when same-zone triggers have equal or missing position metadata.

#### Scenario: Equal position fallback
- **WHEN** two triggered abilities have the same zone priority and equivalent position
- **THEN** ordering is resolved by `ZoneEnterSequence`, then in-card ability order, then runtime registration order

### Requirement: Preserve existing special trigger precedence
The system MUST preserve current special precedence for attacker/defender/event-source before applying generic zone-based ordering.

#### Scenario: Clash precedence remains intact
- **WHEN** attacker and defender both have eligible triggers during a clash-related event
- **THEN** special attacker/defender precedence is applied before zone-position ordering

### Requirement: Unified ordering policy across trigger timings
The system SHALL use the same ordering policy for `Before`, `On`, and `After` trigger collection.

#### Scenario: Timing consistency
- **WHEN** the same set of cards can trigger at different timings for the same event family
- **THEN** each timing applies the same deterministic ordering rules

### Requirement: Consistent zone metadata maintenance
The system MUST keep zone metadata consistent when cards enter or move within tracked zones.

#### Scenario: Re-entry assigns new sequence
- **WHEN** a card leaves a tracked zone and later re-enters
- **THEN** it receives a new monotonic `ZoneEnterSequence` for ordering tie-break

#### Scenario: Zone indices remain contiguous
- **WHEN** cards move, are removed, or inserted in a tracked zone
- **THEN** the zone list is reindexed so `ZoneIndex` stays contiguous from `0..N-1`
