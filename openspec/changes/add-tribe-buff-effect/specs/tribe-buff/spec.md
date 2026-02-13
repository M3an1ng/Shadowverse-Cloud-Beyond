## ADDED Requirements
### Requirement: Tribe Buff Lifecycle
The system SHALL support a tribe buff effect that adds tribe markers to a card through the buff lifecycle.

#### Scenario: Tribe marker is applied when tribe buff is committed
- **WHEN** a buff containing `TribeBuffEffect` is applied to a card
- **THEN** the target card includes the specified tribe marker in its `Tribe` list

#### Scenario: Tribe marker is removed when tribe buff expires or is removed
- **WHEN** a `TribeBuffEffect` buff is removed from a card
- **THEN** the tribe marker contributed by that buff is removed from the card
- **AND** if another active tribe buff still contributes the same marker, the marker remains

### Requirement: Reanimate Uses Tribe Buff
The system SHALL add the reanimate tribe marker through tribe buff processing.

#### Scenario: Reanimate grants reanimate tribe via buff
- **WHEN** Reanimate summons a follower
- **THEN** the summoned follower receives the reanimate tribe marker through a tribe buff event flow
- **AND** the marker is no longer introduced by direct card mutation in the effect handler