## 1. Implementation
- [x] 1.1 Add singleplayer mode toggle (VS-AI vs Dual-Control) and store selection in local handshake state.
- [x] 1.2 Implement dual-control lobby flow: create room, auto-join guest seat, mark ready for both, submit deck selections for both, start match.
- [x] 1.3 Extend SignalR setup to create a secondary connection for the guest seat and expose per-seat connection routing.
- [x] 1.4 Update gameManager to route actions through the active seat connection and to auto-switch localPlayerId when the turn changes.
- [x] 1.5 Update UI gating/hand rendering to reflect the active seat and provide a manual seat toggle in dual-control mode.
- [ ] 1.6 Add regression coverage (manual checklist or automated test hooks) for mulligan, play card, attack, and end turn for both seats.
