## 1. Implementation
- [x] 1.1 Add reconnect UI elements (status banner + button) and styles.
- [x] 1.2 Gate gameplay input while disconnected/reconnecting; re-enable on success.
- [x] 1.3 Implement manual reconnect flow using cached handshake data.
- [x] 1.4 Add hub method to request full game state after reconnect if missing.
- [x] 1.5 Trigger full state sync after reconnect and after manual reconnect.
- [x] 1.6 Add basic integration test or manual QA checklist for reconnect flow.

## 2. QA Checklist
- [ ] 2.1 Disconnect network during a match; reconnect banner/button appears and actions are blocked.
- [ ] 2.2 Restore network; automatic reconnect completes and full state refreshes.
- [ ] 2.3 Click "重新连接" after disconnect; state refreshes and actions resume.
- [ ] 2.4 Verify mulligan phase reconnect still refreshes state and buttons remain locked until sync completes.
