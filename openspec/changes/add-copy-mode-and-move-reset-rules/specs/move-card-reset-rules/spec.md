## ADDED Requirements
### Requirement: Zone-based reset rules for card moves
CardMovedEvent SHALL apply reset rules based on zone transitions.

#### Scenario: Non-reset transitions preserve state
- **WHEN** a card moves Deck -> Hand, Hand -> Deck, Hand -> Playing, Playing -> Field, or Deck -> Field
- **THEN** the card instance retains its current runtime state

#### Scenario: Other transitions reset to a fresh instance
- **WHEN** a card moves between any other zones
- **THEN** the card is reset to a fresh instance of the same definition with no preserved state
