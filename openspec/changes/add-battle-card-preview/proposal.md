# Change: Add Battle Card Preview Panel

## Why
The battle UI lacks a detailed card visualization, while the card editor already provides a polished preview slate. Reusing that preview in battle improves readability and consistency.

## What Changes
- Add a top-left preview panel in the battle UI that renders the same card preview slate as the card editor.
- Update battle UI click handling so clicking a card updates the preview panel.
- Show a buff summary list beneath the preview description, grouped by `EffectKey`.
- Add a buff stat summary line at the top of the buff list showing total stat deltas.
- Surface buff metadata (effect key, source card name, source ability name) in the game state payload for preview rendering.
- Add a preview header "view detail" button that opens the card detail art modal using `ArtDetail`.

## Impact
- Affected specs: battle-card-preview (new)
- Affected code: `index.html`, `src/ui.js`, `src/styles/game.css`, `src/styles/deck-builder.css`, `Server/Dtos/CardInstanceDto.cs`, `Server/Mappings/CardInstanceProfile.cs`
