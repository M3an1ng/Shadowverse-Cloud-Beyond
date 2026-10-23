## ADDED Requirements

### Requirement: Alternate play abilities are effective declaration abilities

The system SHALL treat abilities with `Accelerate` or `Crystalize` keywords and valid alternate play metadata as alternate play declaration abilities. These declaration abilities SHALL be resolved through the same effective ability pipeline as other card abilities so granted, removed, suppressed, and ordered abilities affect availability.

#### Scenario: Suppressed alternate play ability is unavailable
- **WHEN** a card has an Accelerate ability that is suppressed by the effective ability pipeline
- **THEN** the card SHALL NOT expose that Accelerate ability as an available play mode

#### Scenario: Granted alternate play ability is available
- **WHEN** a card gains an effective Crystalize ability with a valid replacement card definition and affordable cost
- **THEN** the card SHALL expose Crystalize as an available play mode when the base card cost is unaffordable

### Requirement: Alternate play candidates require replacement card definitions

The system SHALL only consider an Accelerate or Crystalize ability as an alternate play candidate when the ability has alternate play metadata with a positive replacement card definition ID.

#### Scenario: Missing replacement definition metadata
- **WHEN** a card has an Accelerate keyword ability with no replacement card definition ID
- **THEN** the resolver SHALL ignore that ability as an alternate play candidate

#### Scenario: Invalid replacement definition type
- **WHEN** Accelerate references a non-Spell card definition
- **THEN** committing the play SHALL fail before PP is spent or the card changes zones

#### Scenario: Crystalize replacement must be amulet
- **WHEN** Crystalize references a non-Amulet card definition
- **THEN** committing the play SHALL fail before PP is spent or the card changes zones

### Requirement: Alternate play is only selected when base play is unaffordable

The system SHALL only select Accelerate or Crystalize when the player's spendable PP is lower than the card's effective base cost and at least one alternate play candidate is affordable.

#### Scenario: Base cost is affordable
- **WHEN** the player has enough spendable PP to pay the card's base cost
- **THEN** the resolver SHALL NOT auto-select Accelerate or Crystalize

#### Scenario: Base cost is unaffordable and alternate cost is affordable
- **WHEN** the player cannot pay the card's base cost but can pay an Accelerate cost
- **THEN** the resolver SHALL select Accelerate as the play mode

### Requirement: Alternate play selection follows cost and order priority

When multiple Accelerate and Crystalize candidates are available, the system SHALL select the candidate with the highest effective cost that does not exceed the player's spendable PP. When candidates have the same effective cost, the system SHALL select the candidate that appears earlier in the effective ability list.

#### Scenario: Highest affordable alternate cost wins
- **WHEN** a card has Accelerate 2 and Crystalize 4 and the player has 4 spendable PP while base cost is unaffordable
- **THEN** the resolver SHALL select Crystalize 4

#### Scenario: Same-cost candidates use effective ability order
- **WHEN** a card has Crystalize 3 before Accelerate 3 in the effective ability list and the player has 3 spendable PP while base cost is unaffordable
- **THEN** the resolver SHALL select Crystalize 3

#### Scenario: Unaffordable higher alternate is skipped
- **WHEN** a card has Accelerate 2 and Crystalize 5 and the player has 2 spendable PP while base cost is unaffordable
- **THEN** the resolver SHALL select Accelerate 2

### Requirement: Client requests cannot override alternate play priority

The server SHALL reject a play request that attempts to use a non-selected Accelerate or Crystalize ability when the resolver selects a different alternate play candidate.

#### Scenario: Client submits later same-cost ability
- **WHEN** the resolver selects the earlier Crystalize 3 ability
- **AND** the client submits the later Accelerate 3 ability runtime ID
- **THEN** the request SHALL fail with a selected alternate play priority error

#### Scenario: Client omits alternate ability runtime ID
- **WHEN** the resolver selects an alternate play ability
- **AND** the client submits the selected play mode without an ability runtime ID
- **THEN** the server SHALL bind the selected ability runtime ID from the resolver

### Requirement: Preflight uses replacement card behavior without permanent transform

When a play request selects Accelerate or Crystalize, the pre-commit playability and transaction preflight SHALL evaluate the referenced replacement card definition and its play abilities while preserving the original card instance state until commit.

#### Scenario: Accelerate spell requires target
- **WHEN** a high-cost follower is selected for Accelerate as a replacement spell with a required target
- **THEN** target availability and target selection SHALL use the replacement spell ability configuration

#### Scenario: Alternate play target selection is cancelled
- **WHEN** the player cancels target selection for an alternate play spell
- **THEN** the card SHALL remain in hand with its original definition
- **AND** no PP SHALL be spent
- **AND** no Accelerate or Crystalize event SHALL be emitted

#### Scenario: Replacement preconditions fail
- **WHEN** the selected replacement card definition has a play precondition that is not satisfied
- **THEN** the alternate play mode SHALL be reported as not playable for that request

### Requirement: Commit transforms the same card instance before normal play execution

On successful alternate play commit, the system SHALL permanently transform the same `CardInstance` into the replacement card definition before executing normal play movement and play effects.

#### Scenario: Accelerate resolves as spell
- **WHEN** a card is successfully played with Accelerate referencing a Spell definition
- **THEN** the same card instance SHALL transform to the Spell definition before spell play movement
- **AND** the card SHALL move through the spell playing/graveyard flow as the replacement definition

#### Scenario: Crystalize resolves as amulet
- **WHEN** a card is successfully played with Crystalize referencing an Amulet definition
- **THEN** the same card instance SHALL transform to the Amulet definition before summon/field placement
- **AND** the card SHALL occupy a field slot as the replacement definition

### Requirement: Field-space validation uses replacement card type

The system SHALL use the selected replacement card type for field-space checks when alternate play is selected.

#### Scenario: Full field allows Accelerate spell
- **WHEN** the player's field is full
- **AND** a card can be played as an Accelerate Spell
- **THEN** the playability check SHALL allow the Accelerate mode if all other requirements are met

#### Scenario: Full field blocks Crystalize amulet
- **WHEN** the player's field is full
- **AND** a card can be played as a Crystalize Amulet
- **THEN** the playability check SHALL reject the Crystalize mode

### Requirement: Alternate play emits dedicated success events

After successful alternate play commit, the system SHALL emit a dedicated `Accelerate` or `Crystalize` event containing the player, transformed card instance, paid cost, and triggering ability runtime ID.

#### Scenario: Accelerate emits success event
- **WHEN** a card is successfully played with Accelerate
- **THEN** an `Accelerate` event SHALL be emitted after play state changes are generated

#### Scenario: Crystalize emits success event
- **WHEN** a card is successfully played with Crystalize
- **THEN** a `Crystalize` event SHALL be emitted after play state changes are generated

### Requirement: Replay and logs observe replacement definition after success

After successful alternate play, event history, replay projections, logs, field, and graveyard views SHALL identify the played card by the replacement card definition rather than the original definition.

#### Scenario: Graveyard shows Accelerate spell
- **WHEN** a card is successfully played with Accelerate and moves to graveyard
- **THEN** graveyard views SHALL show the replacement Spell definition

#### Scenario: Field shows Crystalize amulet
- **WHEN** a card is successfully played with Crystalize and enters the field
- **THEN** field views SHALL show the replacement Amulet definition

### Requirement: Editor supports alternate play declaration configuration

The ability editor SHALL provide controls for configuring alternate play metadata on Accelerate and Crystalize declaration abilities and SHALL communicate that normal target/effect authoring belongs on the replacement card definition.

#### Scenario: Configure Accelerate replacement
- **WHEN** an editor user configures an Accelerate ability
- **THEN** the editor SHALL allow selecting a Spell replacement card definition

#### Scenario: Configure Crystalize replacement
- **WHEN** an editor user configures a Crystalize ability
- **THEN** the editor SHALL allow selecting an Amulet replacement card definition

#### Scenario: Declaration ability effect fields are discouraged
- **WHEN** an editor user views an Accelerate or Crystalize declaration ability
- **THEN** the editor SHALL make clear that declaration ability targets and effects are not executed by alternate play
