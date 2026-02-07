# Change: Add Singleplayer Dual-Control Mode

## Why
Singleplayer only supports human vs AI, which limits testing and learning scenarios that require full control of both decks. A dual-control (hotseat) mode lets one player operate both sides using the same flow as VS-AI without introducing AI behavior.

## What Changes
- Add a singleplayer "Dual-Control" option that creates a two-seat room and assigns both seats to the same local client.
- Reuse the existing singleplayer deck selection UI to pick both decks; map the opponent selection to the guest seat.
- Route client actions through the correct seat credentials and update UI/input gating based on the active seat.
- Ensure AI automation is not triggered in dual-control matches.

## Impact
- Specs: control-dual-seats
- Code: src/main.js, src/gameManager.js, src/ui.js
