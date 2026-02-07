## ADDED Requirements
### Requirement: Card packs reference a single card set
Card pack definitions SHALL reference exactly one CardSetDefinition.

#### Scenario: Load pack definition
- **WHEN** a card pack definition is loaded
- **THEN** it includes a CardSetDefinition reference

### Requirement: Card packs are not auto-registered on card save
Saving a card with a new card_set SHALL NOT create a CardPackDefinition.

#### Scenario: Save card with new set
- **WHEN** a card is saved with an unknown card_set abbreviation
- **THEN** a CardSetDefinition is created but no CardPackDefinition is created

## REMOVED Requirements
### Requirement: Card pack autocomplete in editor
**Reason**: Card set input replaces card pack input in the editor.
**Migration**: Provide autocomplete based on CardSetDefinitions instead.

### Requirement: Auto-register new card packs
**Reason**: Card saves only auto-register card sets, not packs.
**Migration**: Manage packs explicitly and link them to sets.

### Requirement: Card pack environment list
**Reason**: Environments belong to card sets for legality checks.
**Migration**: Move environment data to CardSetDefinitions.

### Requirement: Default sequence allocation for auto-registered packs
**Reason**: Auto-registration now applies to card sets instead of packs.
**Migration**: Apply sequence allocation rules to CardSetDefinitions.
