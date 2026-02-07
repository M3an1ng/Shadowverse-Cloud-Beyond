## ADDED Requirements
### Requirement: Owner-Relative Turn Start/End Duration Types
The system SHALL support four owner-relative duration types: owner turn start/end and opponent turn start/end, where owner/opponent is determined by the buff target card''s owner.

#### Scenario: Owner turn end expiry
- **WHEN** a buff with OwnerTurnEnd is applied to a card owned by player A
- **THEN** the buff expires at the end of player A''s turns only

#### Scenario: Opponent turn start expiry
- **WHEN** a buff with OpponentTurnStart is applied to a card owned by player A
- **THEN** the buff expires at the start of player B''s turns only
