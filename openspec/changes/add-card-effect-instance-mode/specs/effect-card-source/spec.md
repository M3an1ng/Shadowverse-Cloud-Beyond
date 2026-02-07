## ADDED Requirements
### Requirement: CardSourceMode for Card Placement Effects
Card placement effects (Summon, GetToken, AddDeck) SHALL expose a CardSourceMode parameter with Definition and Instance options.

#### Scenario: Definition mode creates new cards
- **WHEN** CardSourceMode is Definition for a card placement effect
- **THEN** the system creates new card instances from CardDefinitionId and ignores card targets

#### Scenario: Instance mode moves selected cards
- **WHEN** CardSourceMode is Instance for a card placement effect
- **THEN** the system uses selected card targets as the source cards for placement

### Requirement: Instance Mode Preserves Original Owner
Card placement effects SHALL keep the source card's original owner when executing in Instance mode.

#### Scenario: Instance mode uses original owner
- **WHEN** CardSourceMode is Instance for a card placement effect
- **THEN** the destination player is the selected card's original owner
