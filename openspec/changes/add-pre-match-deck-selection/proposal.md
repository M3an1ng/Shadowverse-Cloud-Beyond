# Change: Add Pre-Match Deck Selection

## Why
Singleplayer and multiplayer flows currently auto-pick a deck, and AI uses the same deck. Players need to choose decks before entering a match, including a separate AI deck for singleplayer.

## What Changes
- Add deck selection controls to the existing multiplayer panel (no new page).
- Add a new singleplayer panel with the same visual style as multiplayer, including deck selection.
- Support separate AI deck selection for singleplayer.
- Use the selected deck IDs when starting matches and submitting deck selection.

## Impact
- Affected capability: pre-match deck selection.
- Affected code: `src/main.js`, `src/router.js`, multiplayer UI panel module, new singleplayer panel markup/styles.
- No API changes expected; reuse existing deck list and match endpoints.
