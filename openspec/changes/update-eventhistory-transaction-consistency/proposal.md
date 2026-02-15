# Change: Update Event History Transaction Consistency

## Why
Continuous abilities that read `context.eventHistory.events` can observe stale data during the same transaction.
This causes one-event lag behavior (for example, costs updating only after another action is performed).

## What Changes
- Move event history persistence to transaction execution timing so committed events are visible within the same transaction flow.
- Keep `EventProcessor` focused on DTO conversion and game-end cleanup instead of history writes.
- Remove `EventHistoryReader` snapshot caching to always read the latest persisted history view.
- Add regression tests for history reader freshness.

## Impact
- Affected code:
  - `Server/Services/GameActionTransactionService.cs`
  - `Server/Domain/Events/EventProcessor.cs`
  - `Server/Domain/Events/EventHistoryReader.cs`
  - `Server.Tests/EventHistoryReaderTests.cs`
