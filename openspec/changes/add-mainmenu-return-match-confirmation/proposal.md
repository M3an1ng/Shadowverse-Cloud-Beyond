# Change: Add main-menu match return confirmation

## Why
The current route cleanup flow immediately leaves a multiplayer room when the player exits `game` or `multiplayer`.  
This prevents a safe "back to menu, then decide" experience and can unintentionally disconnect active matches.

## What Changes
- Add a pending-exit decision flow when navigating from `game` or `multiplayer` to `navigation` with an active non-AI room handshake.
- Show a confirmation prompt on the main menu to let the player choose:
  - return to match, or
  - disconnect from the room.
- Defer `/api/match/{gameId}/leave` and handshake cleanup until the player explicitly confirms disconnect.
- Clear pending decision state automatically when the room is closed by the server.

## Impact
- Affected specs: `game-reconnect`
- Affected code: `src/main.js`, `src/router.js`, menu/prompt UI wiring in frontend
