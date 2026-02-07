## 1. Implementation
- [x] 1.1 Add CardMoveReason enum and expose LastMoveReason on CardInstance (DSL-friendly).
- [x] 1.2 Extend CardMovedEvent + PlayerState move APIs to accept and apply move reason after reset.
- [x] 1.3 Wire move reasons for destroy/discard/burn/countdown/spell-to-graveyard paths.
- [x] 1.4 Update reset ordering so LastMoveReason persists for post-move queries.
- [x] 1.5 Remove DestroyedFollowerRecord tracking and adjust reanimate selection to use graveyard + reason + weighting.
- [x] 1.6 Update tests for move reasons and reanimate weighting using graveyard data.
