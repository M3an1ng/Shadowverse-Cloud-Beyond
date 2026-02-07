## 1. Implementation
- [x] 1.1 Extend CardSourceMode with Copy and add localization.
- [x] 1.2 Add copy support in CardFactory (and CopyCardEvent if needed) to duplicate instances with defined reset rules.
- [x] 1.3 Update Summon/GetToken/AddDeck handlers to support CardSourceMode.Copy and PlayerScope ownership.
- [x] 1.4 Implement CardMovedEvent reset rules for zone transitions and state reset behavior.
- [x] 1.5 Add tests for copy mode (state preservation + owner assignment) and move reset rules.
