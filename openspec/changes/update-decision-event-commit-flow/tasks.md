## 1. Implementation
- [x] 1.1 Add `ChooseEvent` and `RandomEvent` with commit-time output population.
- [x] 1.2 Update raw event mapping and any DTO/broadcast mapping for decision events.
- [x] 1.3 Pre-resolve cancellable Choose/Random in Phase 1 and reuse selections in commit.
- [x] 1.4 Move decision resolution into effect execution and emit decision events before branch ability execution.
- [x] 1.5 Ensure branch abilities execute within the same transaction.
- [x] 1.6 Remove decision output merging into EffectCompletedEvent outputs.
- [x] 1.7 Enforce commit-phase Choose as non-cancellable.
- [x] 1.8 Update Choose cancellation to return a cancelled transaction result during Phase 1.
- [x] 1.9 Add tests for decision events and transaction behavior (commit outputs, cancel path).
