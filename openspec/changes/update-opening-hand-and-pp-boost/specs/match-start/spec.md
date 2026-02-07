## ADDED Requirements
### Requirement: Opening hand size and mulligan
The system SHALL deal 4 cards to each player at match start and SHALL allow each player to mulligan any subset of those 4 cards. The final hand after mulligan SHALL contain 4 cards.

#### Scenario: Initial deal is four cards
- **WHEN** the match starts and the mulligan phase begins
- **THEN** each player hand size is 4 before mulligan choices are submitted

#### Scenario: Mulligan preserves hand size
- **WHEN** a player replaces any subset of their opening hand during mulligan
- **THEN** the player ends the mulligan with 4 cards in hand

### Requirement: No second-player bonus draw
The system SHALL NOT grant an extra opening draw to the second player.

#### Scenario: Second player draws no bonus card
- **WHEN** the match starts
- **THEN** the second player has exactly 4 cards in hand