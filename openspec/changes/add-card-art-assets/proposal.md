# Change: Add card art assets (preview/detail)

## Why
- The card editor ASSETS tab is still a placeholder and cannot persist art assets.
- Deck builder needs a way to show a full card detail image on demand.

## What Changes
- Add optional art fields (artPreview/artDetail) to card definitions and DTOs.
- Provide upload endpoints to save preview/detail images separately.
- Persist file paths and surface artPreview in the card preview UI.
- Allow deck builder to open a detail image on click.

## Impact
- Affected specs: card-editor
- Affected code:
  - Backend: Server/Domain/Entities/CardDefinition.cs, Server/Dtos/CardDefinitionDto.cs, Server/Dtos/CardUpsertDto.cs, Server/Mappings/CardDefinitionProfile.cs, Server/Controllers/CardsController.cs, Server/Infrastructure/Persistence/Migrations/*
  - Frontend: src/cardEditor.js, src/deckBuilder.js, src/styles/card-editor.css, src/styles/deck-builder.css
