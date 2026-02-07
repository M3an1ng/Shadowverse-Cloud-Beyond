## ADDED Requirements
### Requirement: EffectCompletedEvent exposes generated card snapshots
The system SHALL expose a GeneratedCardSnapshots parameter on EffectCompletedEvent that captures immutable card property snapshots at effect completion time.

#### Scenario: Snapshot survives zone reset
- **WHEN** an effect generates a card and a later commit resets or moves that card
- **THEN** GeneratedCardSnapshots still reflect the original card properties captured at completion time

#### Scenario: No generated cards
- **WHEN** an effect does not generate any cards
- **THEN** GeneratedCardSnapshots is an empty list