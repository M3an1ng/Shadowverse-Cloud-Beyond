## ADDED Requirements
### Requirement: LeaveVanish keyword
The system SHALL support a LeaveVanish keyword that converts a leave/destroy into a vanish (Removed) result.

#### Scenario: LeaveVanish converts destroy to vanish
- **WHEN** a card with LeaveVanish is destroyed
- **THEN** the card moves to Removed and a vanish event is emitted

#### Scenario: LeaveVanish converts any leave from the battlefield to vanish
- **WHEN** a card with LeaveVanish would leave the battlefield for Hand or Deck
- **THEN** the card moves to Removed and a vanish event is emitted

#### Scenario: LeaveVanish suppresses original leave events
- **WHEN** a card with LeaveVanish would return to Hand or be added to Deck
- **THEN** the original leave event is not emitted and a vanish event is emitted

#### Scenario: LeaveVanish suppresses move-derived updates
- **WHEN** a card with LeaveVanish would move from the battlefield to another zone
- **THEN** only a vanish event is emitted and no follow-up move-derived updates are triggered

#### Scenario: LeaveVanish does not add graveyard count
- **WHEN** a card with LeaveVanish leaves play
- **THEN** the owning player's graveyard counter does not increase
