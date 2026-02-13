# Design: Frontend Zone Movement via CardMoved

## Context
前端当前存在“语义事件 + 快照事件”同时修改区域集合的问题。该设计将区域集合更新集中到 CardMoved，以提高一致性与可推理性。

## Goals
1. Zone mutation single-writer: only CardMoved changes card zone membership.
2. Semantic events remain available for UX (animation/log/announcement) and triggers.
3. Preserve reconnect/catch-up correctness with snapshot reconciliation.

## Non-Goals
1. 不改变领域层触发语义（Return/Destroy/Vanish/Draw 的业务定义保持不变）。
2. 不在本变更内重构所有日志与动画系统。

## Architecture
1. Server Event Layer
- `CardMovedEvent` emits a dedicated client DTO with minimum deterministic movement fields.
- Semantic events still emit DTOs but are marked as non-mutating for zone state.

2. Client State Layer
- `handleCardMoved(event)` executes the canonical move algorithm:
  - locate card by instance id in all owner zones
  - remove from source zone if present
  - insert/update in destination zone
  - update card.zone / owner linkage
- all other handlers MUST NOT mutate zone collections.

3. Snapshot Reconciliation
- `HandUpdated` only replaces hand snapshot for its owner.
- `ReceiveGameState` remains full authoritative reset path.
- Optional mismatch detector logs when semantic event implies movement without paired CardMoved.

## Ordering and Idempotency
- Each movement event SHOULD contain stable `eventId` and monotonic sequence/revision.
- Client keeps a bounded dedupe cache and discards already-processed movement events.
- On detected gaps/out-of-order, client requests/awaits next snapshot sync.

## Migration Plan
1. Phase A: Emit CardMoved DTO + keep legacy mutations (observe only).
2. Phase B: Enable CardMoved-only mutations behind feature flag.
3. Phase C: Remove legacy mutation paths and keep semantic handlers pure.

## Test Strategy
1. Transactional movement cases:
- Field -> Hand (Return)
- Field -> Graveyard (Destroy)
- Any -> Removed (Vanish)
- Deck -> Hand (Draw)
2. Robustness cases:
- duplicate CardMoved delivery
- out-of-order delivery
- reconnect snapshot correction
