## 1. Gesture State And Submission Flow

- [x] 1.1 Add a dedicated hand-card drag state container and pointer lifecycle handlers in `src/ui.js`
- [x] 1.2 Replace double-click hand-card play as the primary trigger with drag-threshold activation while preserving click-to-preview behavior
- [x] 1.3 Resolve valid hand-card drops through the existing `playCard` submission path and suppress release-triggered follow-up clicks
- [x] 1.4 Route target-requiring drag releases to direct-target submission only when legal, otherwise hand off to the existing player-choice flow

## 2. Visual Feedback And UX

- [x] 2.1 Render a transient dragged-card ghost plus source-slot drag-origin state using the existing transient visual layer
- [x] 2.2 Add valid/invalid drop guidance and invalid-drop snap-back styles in `src/styles/game.css`
- [x] 2.3 Reuse existing target-highlight states for drag hover feedback without disturbing attack drag visuals
- [x] 2.4 Add reduced-motion handling for drag feedback while keeping validity and release-state cues intact

## 3. Verification

- [x] 3.1 Add front-end regression tests covering click preview versus drag activation and single-request submission
- [x] 3.2 Add regression coverage for target-requiring cards, invalid drops, and fallback button play
- [ ] 3.3 Run a manual battle smoke test for mouse-driven drag play, invalid snap-back, and target-choice handoff
