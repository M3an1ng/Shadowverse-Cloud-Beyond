## 1. Implementation
- [x] 1.1 Add a fixed turn timeout constant (180s) and expose timing metadata in game state/TurnStarted event.
- [x] 1.2 Implement server turn-timer lifecycle (start on TurnStart, stop on turn end/game end).
- [x] 1.3 Implement timeout handling to force-resolve pending choices and auto-end the turn.
- [x] 1.4 Update client state handling to consume timing metadata and drive a countdown.
- [x] 1.5 Add end-turn button UI for numeric countdown + progress bar animation.
- [x] 1.6 Add tests or verification notes for timeout and pending choice resolution.

## 2. Verification
- [ ] 2.1 Start a match and confirm the end-turn button shows 03:00 with a draining progress bar during the local turn.
- [ ] 2.2 Let the timer expire and confirm the turn auto-ends on the server.
- [ ] 2.3 Trigger a non-cancellable choice, let the timer expire, and confirm a random legal selection resolves before auto-ending.
