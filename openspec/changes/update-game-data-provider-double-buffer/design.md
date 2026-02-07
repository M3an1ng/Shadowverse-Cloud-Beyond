## Context
GameDataProvider currently clears cache dictionaries before reloading, which creates a window where runtime reads return empty data. This causes inconsistent behavior when refreshing data during active sessions.

## Goals / Non-Goals
- Goals: Provide an atomic cache refresh that avoids empty-cache windows while keeping refresh latency reasonable.
- Non-Goals: Isolate cache versions per match or prevent running matches from seeing new data.

## Decisions
- Decision: Use a CacheSnapshot object holding all dictionaries and statistics; build a new snapshot off the hot path and swap it via Interlocked.Exchange.
- Decision: Reads always reference the current snapshot; no partial writes to live dictionaries.
- Decision: RefreshCacheAsync replaces the snapshot after all data is loaded successfully.

## Risks / Trade-offs
- Memory usage briefly doubles during refresh (old + new snapshot).
- Running matches may observe new data after the swap.

## Migration Plan
- No data migration required; cache refresh remains triggered by the existing diagnostics endpoint.

## Open Questions
- Should we expose snapshot version/timestamp for diagnostics?
