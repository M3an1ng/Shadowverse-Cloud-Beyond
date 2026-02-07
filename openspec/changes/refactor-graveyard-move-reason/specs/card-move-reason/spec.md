## ADDED Requirements
### Requirement: Track card move reason
The system SHALL record the reason for a card's most recent zone change, and expose it to DSL queries.

#### Scenario: Graveyard move reason recorded
- **WHEN** a card enters the Graveyard due to destruction
- **THEN** the card's LastMoveReason SHALL be set to Destroyed and accessible in DSL

#### Scenario: Transform move reason recorded
- **WHEN** a card is moved into the Graveyard due to a transformation sequence
- **THEN** the card's LastMoveReason SHALL be set to Transform and accessible in DSL

### Requirement: Move reason applied after reset
The system SHALL apply LastMoveReason after card reset so the reason is preserved for post-move queries.

#### Scenario: Reset does not erase last move reason
- **WHEN** a card is moved into the Graveyard and reset to definition state
- **THEN** LastMoveReason SHALL still reflect the move reason after the move completes
