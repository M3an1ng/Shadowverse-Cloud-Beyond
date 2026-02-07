# Change: Normalize card pack identifiers and environment

## Why
Card packs are currently stored as free-form strings, which prevents consistent ordering, abbreviations, and format-scoped availability. We need structured identifiers and environment linkage while preserving existing data.

## What Changes
- Add card pack fields for region, sequence, abbreviation, and DeckEnvironment linkage.
- Define a 3-digit pack code derived from region and sequence, with uniqueness enforcement.
- Link card definitions to card pack definitions as the source of truth for pack metadata.
- Update seeding and APIs to expose pack metadata for filtering and display.

## Impact
- Affected specs: card-pack-catalog
- Affected code: Server/Domain/Entities/CardPackDefinition.cs, Server/Domain/Entities/CardDefinition.cs, Server/Infrastructure/Data/ApplicationDbContext.cs, Server/Infrastructure/Data/DataSeeder.cs, Server/Dtos/CardPackDefinitionDto.cs, Server/Dtos/CardDefinitionDto.cs, Server/Dtos/CardUpsertDto.cs, Server/Services/EconomyService.cs, src/cardEditor.js, src/deckBuilder.js
