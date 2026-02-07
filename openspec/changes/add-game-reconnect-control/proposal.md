# Change: Add game reconnect control

## Why
Players can lose interaction after refresh or transient disconnects. The client should expose a clear reconnect path and re-sync game state to prevent UI dead-ends.

## What Changes
- Add a reconnect status banner and manual reconnect button during disconnect/reconnect states.
- Lock gameplay interactions while disconnected or reconnecting.
- After reconnect, request and apply a full game state update.

## Impact
- Affected specs: game-reconnect
- Affected code: src/main.js, src/ui.js, src/gameManager.js, Server/SignalR game hub
