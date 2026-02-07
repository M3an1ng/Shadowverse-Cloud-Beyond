## 1. Implementation
- [x] 1.1 Define snapshot model (DTO or lightweight struct) for generated cards.
- [x] 1.2 Add snapshot field to EffectCompletedEvent and expose via event parameters/DSL.
- [x] 1.3 Populate snapshots when EffectCompletedEvent is created in EffectExecutor.
- [x] 1.4 Update raw event mapping/serialization if required.
- [x] 1.5 Add tests to ensure snapshots remain stable after zone moves/resets.
