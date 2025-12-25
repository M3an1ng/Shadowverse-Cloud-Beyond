## 1. Backend Metadata
- [x] 1.1 Add `CategoryId`/`CategoryLabel`/`Visibility` to `EventMetadataDto` and `EffectUIMetadataDto`.
- [x] 1.2 Extend `GameEventMetadataAttribute` to accept category + visibility; update `EventMetadataService` to populate new fields with defaults.
- [x] 1.3 Extend `EffectMetadataAttribute` to accept category; update `EffectUIMetadataService` to populate new fields with defaults.
- [x] 1.4 Annotate existing event metadata with category/visibility per the player-facing mapping (ActionRequests => `Visibility=System`).
- [x] 1.5 Annotate existing effect metadata with category per the player-facing mapping.

## 2. Frontend (Ability Editor)
- [x] 2.1 Update ability editor metadata parsing to include category/visibility fields.
- [x] 2.2 Group event selector entries by category; hide `Visibility=System|Hidden` by default; add a toggle to show system events.
- [x] 2.3 Group effect selector entries by category in the effect picker UI.
- [x] 2.4 Add stable category ordering: 基础行动 → 位置变化 → 临时状态 → 资源数值 → 回合流程 → 特殊事件 → 系统请求 → 未分类。

## 3. Tests
- [x] 3.1 Add backend tests to verify event metadata includes category + visibility for a normal event and an ActionRequest event.
- [x] 3.2 Add backend tests to verify effect metadata includes category.
- [x] 3.3 Add frontend tests to verify grouping + system toggle behavior (smoke-level).

## 4. Documentation
- [x] 4.1 Document the player-facing categories and visibility rules in the editor metadata docs.
