# Change: Card Code and Versioning

## Why
- Provide stable card identity across versions while keeping player decks always up to date.
- Allow card editor to save new versions without mutating historical data.
- Preserve match/replay fidelity via immutable snapshots.

## What Changes
- Introduce 8-digit card Code format and add CardVersion for version tracking.
- Save new versions as new CardDefinition records (old versions become immutable).
- Defer ability/art persistence until card save and scope them per version.
- Store decks by Code and resolve to latest version at play time; add deck code v2.
- Persist match/replay deck snapshots for deterministic playback.

## Impact
- Affected specs: manage-card-versions
- Affected code: Server/Domain/Entities/CardDefinition.cs, Server/Infrastructure/Data/ApplicationDbContext.cs, Server/Dtos/CardDefinitionDto.cs, Server/Dtos/CardUpsertDto.cs, Server/Mappings/CardDefinitionProfile.cs, Server/Controllers/CardsController.cs, Server/Services/DeckService.cs, Server/Services/DeckCodeService.cs, Server/Services/GameDataProvider.cs, Server/Services/GameService.cs, src/cardEditor.js, src/deckBuilder.js
