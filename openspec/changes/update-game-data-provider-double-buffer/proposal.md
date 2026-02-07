# Change: Double-Buffer GameDataProvider Cache Refresh

## Why
Refreshing the GameDataProvider cache currently clears all dictionaries before reloading. During that window, runtime reads can return empty results, which causes incorrect behavior in live matches. We need a safe refresh mechanism so configuration edits can take effect for new matches without restarting the server.

## What Changes
- Build a new cache snapshot in the background and atomically swap it in after it is fully loaded.
- Ensure cache reads always use a fully populated snapshot (no empty-cache window).
- Keep current behavior where in-progress matches may observe new definitions after refresh.

## Impact
- Affected specs: game-data-cache (new capability).
- Affected code: Server/Services/GameDataProvider.cs, Server/Services/Abstractions/IGameDataProvider.cs (if stats exposure changes), Server/Controllers/DiagnosticsController.cs.
