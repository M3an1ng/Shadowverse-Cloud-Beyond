## ADDED Requirements
### Requirement: CardMoved is the single source of zone mutation on client
The client MUST apply card zone membership changes only from CardMoved events.

#### Scenario: Return movement
- **WHEN** a card is returned from field to hand
- **THEN** the client SHALL move the card from field collection to hand collection when processing CardMoved(Field->Hand)
- **AND** no other event handler SHALL perform this zone mutation

#### Scenario: Destroy movement
- **WHEN** a card is destroyed from field
- **THEN** the client SHALL move the card from field collection to graveyard collection when processing CardMoved(Field->Graveyard)

### Requirement: Semantic events are non-mutating for zone state
Semantic events (including Return, Destroy, Vanish, Draw) MUST NOT directly mutate client zone collections.

#### Scenario: Semantic event arrives before movement event
- **WHEN** a semantic event is received without an already-applied CardMoved
- **THEN** the client SHALL only perform UX/log updates
- **AND** the zone state SHALL remain unchanged until CardMoved is processed

### Requirement: HandUpdated is a hand snapshot sync only
HandUpdated MUST synchronize only the hand snapshot and MUST NOT clear or move cards in non-hand zones.

#### Scenario: Hand snapshot refresh
- **WHEN** HandUpdated is received for a player
- **THEN** the client SHALL replace that player's hand snapshot
- **AND** SHALL NOT mutate field/emblem/graveyard collections in the same handler

### Requirement: Incremental event idempotency for movement
The client MUST guarantee idempotent CardMoved processing using event identity.

#### Scenario: duplicate movement delivery
- **WHEN** the same CardMoved event is delivered more than once
- **THEN** the client SHALL apply the movement at most once
- **AND** subsequent duplicates SHALL be ignored
