## 1. Implementation
- [ ] 1.1 Add/standardize `CardMoved` client DTO contract (from/to/moveReason/cardId/ownerId/eventId/sequence).
- [ ] 1.2 Ensure server maps `CardMovedEvent` to dedicated client event channel and keeps semantic events separate.
- [ ] 1.3 Implement `handleCardMoved` as the only zone-mutation entry in frontend state layer.
- [ ] 1.4 Remove zone mutation from `handleHandUpdated` and semantic handlers (`handleUnitDestroyed`, `handleCardVanished`, `handleCardReturned`, draw-related handlers).
- [ ] 1.5 Add idempotency + ordering guard for incremental events.
- [ ] 1.6 Add integration scenarios for Return/Destroy/Vanish/Draw zone transitions.

## 2. Rollout
- [ ] 2.1 Introduce compatibility flag for dual-path observation in non-production matches.
- [ ] 2.2 Verify no visual regressions in mulligan, reconnect, and replay/catch-up flows.
- [ ] 2.3 Remove temporary compatibility writes once CardMoved-only flow is stable.
