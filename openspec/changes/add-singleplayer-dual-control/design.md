## Context
- Singleplayer creates a room with useAiFallback=true and no guest seat; AI actions are triggered when PlayerState.IsAI is true.
- Client actions are authorized per SignalR connection via playerId/token query params.
- UI input gating relies on gameState.localPlayerId and the current turn player.

## Goals / Non-Goals
- Goals: allow a single client to control both seats; reuse the singleplayer deck selection flow; no AI automation in this mode.
- Non-Goals: new AI behaviors, changing PvP matchmaking, replay format changes.

## Decisions
- Use existing lobby endpoints: create a normal (non-AI) room, auto-join as guest using join code, and submit deck selections for both seats.
- Maintain two SignalR connections (host + guest). The primary connection receives state updates; the secondary is used to send actions for the guest seat.
- Add a dual-control flag to the local handshake and game state; use it to route actions and to switch the controlled seat.
- Switch the controlled seat automatically to the current turn player; no manual seat toggle in dual-control.
- Render both hands in dual-control mode; do not hide the opponent hand.

## Risks / Trade-offs
- Two connections will both receive broadcast events; avoid duplicate processing by wiring handlers only on the primary connection.
- Seat switching changes localPlayerId, which affects UI layout; ensure swaps are predictable and surfaced to the user.
- Storing guest credentials in local storage increases local state complexity; clear on leave/cleanup.

## Migration Plan
- No data migration.
- Add a new singleplayer mode option and keep existing VS-AI as default.

## Open Questions
- None.
