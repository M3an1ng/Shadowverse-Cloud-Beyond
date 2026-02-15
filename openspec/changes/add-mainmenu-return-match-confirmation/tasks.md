## 1. Implementation
- [x] 1.1 Add pending room-exit decision state for active non-AI multiplayer handshakes.
- [x] 1.2 Hook route transitions (`game|multiplayer -> navigation`) to mark decision pending instead of immediately leaving the room.
- [x] 1.3 Add prompt UI and action handlers on navigation to support "Return to Match" and "Disconnect".
- [x] 1.4 Update leave flow so `/leave` and cached handshake cleanup only happen after explicit disconnect confirmation (or terminal room closure).
- [x] 1.5 Add edge-case guards for duplicate prompts, fast repeated navigation, and stale/closed rooms.
- [x] 1.6 Add/adjust tests for leave gating and return-path behavior.

## 2. QA Checklist
- [ ] 2.1 From `game` to `navigation` with an active PVP room, prompt appears exactly once.
- [ ] 2.2 Choose "Return to Match": app navigates back to `game` and does not call `/leave`.
- [ ] 2.3 Choose "Disconnect": app calls `/leave` and clears cached handshake.
- [ ] 2.4 After disconnect, returning to `game` does not resume the old room.
- [ ] 2.5 If room closes server-side before decision, prompt is cleared and return action is blocked.
