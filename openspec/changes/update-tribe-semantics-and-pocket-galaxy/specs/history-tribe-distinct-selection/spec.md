## ADDED Requirements

### Requirement: Leave-field tribe candidate selection uses event history snapshots
The system SHALL derive leave-field tribe selection candidates from event history snapshots rather than from graveyard contents or destroy-only projections.

#### Scenario: Leave-field follower contributes candidate data from snapshot
- **WHEN** a follower card leaves the field during a match
- **THEN** the candidate selection flow SHALL be able to read that card's name and `tribe` values from the leave-field event snapshot

#### Scenario: Non-destroy leave-field events remain eligible history sources
- **WHEN** a follower leaves the field by a non-destroy leave-field route that still records a leave-field snapshot
- **THEN** the card SHALL remain eligible for effects that reference "cards that had tribe when leaving the field this match"

### Requirement: Multi-tribe cards expand into semantic tribe candidates
The system SHALL expand each historical leave-field card into semantic tribe candidates before applying tribe-based distinct selection.

#### Scenario: Multi-tribe card contributes one candidate per effective tribe
- **WHEN** a historical leave-field card effectively has multiple tribes
- **THEN** the candidate builder SHALL expand that card into one candidate per effective tribe

#### Scenario: Match-all special tribe expands against current known normal tribes
- **WHEN** a historical leave-field card effectively has a configured match-all special `tribe`
- **THEN** the candidate builder SHALL expand that card against the registry's current known normal tribes

### Requirement: Distinct tribe selection consumes the selected card and all of its effective tribes
The system SHALL prevent a selected historical card from contributing additional candidates after it has been chosen once.

#### Scenario: Selected multi-tribe card blocks all of its tribes
- **WHEN** a card with multiple effective tribes is chosen during a tribe-distinct selection flow
- **THEN** the system SHALL mark all of that card's effective tribes as already used
- **AND** later selections SHALL exclude any candidate whose effective tribe overlaps with that used set

#### Scenario: Selected card cannot be chosen again through another tribe candidate
- **WHEN** a historical card has already been chosen through one of its expanded tribe candidates
- **THEN** all remaining expanded candidates for that same historical card SHALL be excluded from further selection
