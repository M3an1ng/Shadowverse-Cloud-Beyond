## 1. Implementation
- [x] 1.1 Update EffectCompletedEvent to expose generated events and remove GeneratedCards usage.
- [x] 1.2 Update EffectExecutor to pass generated events to EffectCompletedEvent and remove CollectGeneratedCards logic.
- [x] 1.3 Expose resolved ability target pools on GameContext as `resolvedTargets` for DSL access.
- [x] 1.4 Remove `previous` target key injection and related logic.
- [x] 1.5 Update any references/tests/documentation impacted by the payload and target changes.