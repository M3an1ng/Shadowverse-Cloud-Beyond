## 1. Implementation
- [ ] 1.1 Update match start draw to 4 cards each; remove the second-player bonus draw.
- [ ] 1.2 Ensure mulligan uses only the opening 4 cards and ends with a 4-card hand.
- [ ] 1.3 Add PP+1 boost availability and usage tracking to player state and DTOs.
- [ ] 1.4 Add action request and domain event for PP+1 boost; allow activation only during the second player\'s turn and let current PP exceed max for the turn.
- [ ] 1.5 Implement the turn-start reset of PP+1 availability at the start of the second player\'s turn 6 (even if unused).
- [ ] 1.6 Update UI to display PP+1 availability and provide activation controls for the second player.
- [ ] 1.7 Update AI decision logic to account for PP+1 boost.
- [ ] 1.8 Add tests for opening hand size, mulligan replacement, PP boost usage, overflow PP, and turn-6 reset.
- [ ] 1.9 Update logs/metadata as needed for event visibility.
