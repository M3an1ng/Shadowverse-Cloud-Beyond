## Context

The current battle UI uses `DOUBLE_CLICK_WINDOW_MS` and `lastCardClick` in `src/ui.js` to detect double-click hand-card play, while attack interactions already use a pointer-driven `attackDragState` plus click suppression. That split creates two problems: hand-card play feels less direct than attack, and the click handler now has to infer both preview intent and play intent from the same gesture family.

This change only affects front-end battle interaction. The authoritative play pipeline already exists in `src/gameManager.js` via `playCard(cardUid, targetUid)`, and target follow-up already exists through the current `PlayerChoiceRequested` UI flow. The design should therefore replace the gesture layer without introducing a parallel rules engine.

## Goals / Non-Goals

**Goals:**
- Replace double-click hand-card play with a pointer drag interaction that feels consistent with battle attack drag.
- Preserve single-click hand-card preview semantics.
- Reuse the existing `playCard` request path and the existing target-selection flow.
- Provide clear valid/invalid release feedback and prevent duplicate submissions from release clicks.
- Keep the implementation small enough to live inside the current battle UI architecture.

**Non-Goals:**
- No server-side rules changes, DTO changes, or SignalR contract changes.
- No rewrite of the battle preview, choice overlay, or attack drag system.
- No new animation framework or third-party drag library.
- No expansion into unrelated combat animations or board layout redesign.

## Decisions

- Decision: Introduce a dedicated `handDragState` in `src/ui.js` that parallels `attackDragState` instead of extending the existing double-click path.
  - Rationale: Dragging is a pointer lifecycle problem, not a click-timing problem. Keeping it in a dedicated state machine reduces accidental coupling with preview clicks and avoids piling more conditions into `handleGameClick`.
  - Alternative considered: Reinterpret double-click tracking plus mousemove inside the existing click handler. Rejected because it mixes incompatible interaction models and increases duplicate-submit risk.

- Decision: Preserve click-to-preview and require a movement threshold before drag play becomes active.
  - Rationale: Players still need low-friction inspection without accidental plays. A threshold also matches the existing attack drag model and reduces jitter from small pointer movement.
  - Alternative considered: Make any pointerdown on a playable hand card immediately enter drag mode. Rejected because it would degrade preview usability and feel sticky on touchpads.

- Decision: Reuse `ensureTransientVisualRoot`, CSS transforms, and requestAnimationFrame-friendly updates for the dragged-card visual.
  - Rationale: The battle UI already has a transient visual layer and reduced-motion helpers. Reusing them keeps rendering isolated from normal layout and avoids expensive rerenders during pointermove.
  - Alternative considered: Drag the original hand DOM node in-place. Rejected because it destabilizes hand layout, complicates hover styles, and makes snap-back harder to reason about.

- Decision: Successful drops must always route through the existing `playCard` pipeline, with direct target submission only when the release target is already legal.
  - Rationale: Play legality and target validation already live behind the current front-end/server flow. Reusing that path avoids duplicating target resolution logic client-side.
  - Alternative considered: Build a local drag-only targeting workflow that resolves all target cases before submission. Rejected because it would fork the current player-choice system and make legality drift more likely.

- Decision: Completed drags suppress the release click using the same style of suppression already used by attack drag.
  - Rationale: Without suppression, a single drag can still trigger preview selection or a second play path on release.
  - Alternative considered: Depend on event ordering alone. Rejected because browser click synthesis after pointerup is inconsistent enough to create regressions.

- Decision: Keep the existing hand-card action button as an explicit fallback and simplify drag-only flourish when reduced motion is active.
  - Rationale: Drag should become the primary path, not the only path. The fallback reduces accessibility and device-edge regressions while keeping the spec compatible with reduced-motion environments.
  - Alternative considered: Remove the button once drag exists. Rejected because it would narrow input affordances without functional gain.

## Risks / Trade-offs

- [Pointer-release edge cases on touch devices or trackpads] -> Reuse the attack drag threshold and click suppression pattern, and verify with manual battle smoke tests on both mouse and touch-capable browsers.
- [Drag feedback becoming visually noisy in the already-dense battle UI] -> Limit visuals to one transient ghost, one origin state, and one valid/invalid drop indicator rather than introducing multiple simultaneous overlays.
- [Targeted spells creating ambiguous release behavior] -> Prefer direct target submission only for clearly valid targets and otherwise fall back to the existing `PlayerChoiceRequested` path.
- [Interaction regressions in preview or attack flow] -> Keep hand-card drag isolated in its own state container and add regression tests around click preview, duplicate-submit prevention, and fallback button play.

## Migration Plan

- Implement the hand drag state and pointer handlers behind the current battle UI without changing network contracts.
- Remove double-click as the primary hand-card play trigger once drag behavior is in place.
- Keep the existing hand-card action button and current target-choice overlay unchanged as compatibility paths.
- Validate with front-end tests plus a manual battle smoke pass covering click preview, valid drag play, invalid snap-back, and target-requiring cards.

## Open Questions

- None. The first implementation will support direct target submission only when release lands on a clearly valid target; all other target-requiring cases will fall back to the existing player-choice flow.
