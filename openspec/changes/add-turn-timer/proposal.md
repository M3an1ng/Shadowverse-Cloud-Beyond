# Change: Add turn timer and timeout handling

## Why
Players can currently stall a turn indefinitely. A server-authoritative timer is required to enforce a fixed per-turn limit and keep matches moving.

## What Changes
- Add a server-side turn timer starting at TurnStart with a fixed 180-second limit.
- On timeout, force-resolve pending choices (cancel if allowed; otherwise pick random legal targets) and end the turn.
- Send turn timing metadata to clients so the UI can render a countdown and progress bar on the end-turn button.

## Impact
- Affected capability: turn-timer
- Server: GameService, GameConstants, PendingActionService, DTOs/mappings, hub event payloads
- Client: src/gameManager.js, src/ui.js, src/styles/layout.css
