# Change: Update Hand Card Drag Play

## Why
The battle UI still relies on double-click to play a hand card, which is easy to miss, conflicts with card preview intent, and feels inconsistent next to the existing drag-based attack interaction. We need a smoother primary gesture that better communicates when a card is being prepared, aimed, and released.

## What Changes
- Replace double-click hand-card play with drag-to-play as the primary battle interaction for playable hand cards.
- Preserve single-click hand-card preview behavior so inspection and selection still work without accidental play submissions.
- Add transient drag feedback for hand cards, including a lifted drag ghost, valid/invalid drop guidance, and invalid-drop snap-back.
- Route successful drops through the existing `playCard` and `PlayerChoiceRequested` pipelines so play legality and target resolution remain server-authoritative.
- Suppress release-triggered click fallout so a completed drag does not also re-enter click or double-click handling.
- Keep the existing hand-card action button as a fallback path for non-drag use cases.

## Capabilities

### New Capabilities
- `battle-hand-drag-play`: Defines drag-based hand-card play, drag feedback, drop resolution, target handoff, and fallback interaction behavior in the battle UI.

### Modified Capabilities

## Impact
- Affected code: `src/ui.js`, `src/gameManager.js`, `src/styles/game.css`, related battle UI tests under `tests/`
- Affected systems: battle interaction model, hand-card UI feedback, battle target selection handoff
- Dependencies: no new runtime dependencies; reuse existing browser pointer events, battle UI rendering, and `playCard` request flow
