## ADDED Requirements
### Requirement: Copy mode for card placement effects
Card placement effects (Summon, GetToken, AddDeck) SHALL expose CardSourceMode.Copy to duplicate existing card instances.

#### Scenario: Copy mode duplicates selected cards
- **WHEN** CardSourceMode is Copy and card targets are provided
- **THEN** the system creates new card instances by copying the selected cards

#### Scenario: Copy mode uses PlayerScope
- **WHEN** CardSourceMode is Copy
- **THEN** the new copies are owned by the players resolved from PlayerScope (not the original owners)

### Requirement: Copy state preservation rules
Copy mode SHALL preserve all runtime state except attack/act availability.

#### Scenario: Copy mode resets attack and act availability
- **WHEN** a card instance is copied
- **THEN** attack availability and act usage state are reset
- **AND** all other card state remains identical to the source instance
