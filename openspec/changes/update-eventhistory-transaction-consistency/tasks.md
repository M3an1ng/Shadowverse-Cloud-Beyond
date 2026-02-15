## 1. Implementation
- [x] Move event history write timing into `GameActionTransactionService` transaction execution flow.
- [x] Ensure committable events are written after successful commit and before `After` trigger evaluation.
- [x] Ensure non-committable events are written before `After` trigger evaluation.
- [x] Ensure synthetic `CardInstanceUpdatedEvent` records are also persisted to history.
- [x] Remove event history write responsibility from `EventProcessor`.
- [x] Keep game-end history cleanup behavior.
- [x] Remove `EventHistoryReader` in-memory snapshot cache.

## 2. Tests
- [x] Add regression test proving `EventHistoryReader` returns latest history after additional writes.

## 3. Validation
- [x] Run affected backend test suite.
