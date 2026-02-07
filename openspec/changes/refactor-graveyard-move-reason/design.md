## Context
Reanimate selection currently relies on DestroyedFollowerRecord, which is not DSL-visible and cannot be adjusted by abilities. The system already resets cards on zone moves, so we can safely keep a move-reason trail on CardInstance for graveyard queries.

## Goals / Non-Goals
- Goals:
  - Expose last move reason for DSL queries against graveyard cards.
  - Replace destroyed-history registry with graveyard-based selection and explicit move reasons.
- Non-Goals:
  - Implement full event interception or modify historical event logs.
  - Change the meaning of non-graveyard zones beyond carrying LastMoveReason.

## Decisions
- Store LastMoveReason on CardInstance and update it via CardMovedEvent after reset.
- CardMovedEvent carries a move reason for all move paths that hit Graveyard.
- Reanimate selection uses graveyard cards filtered by LastMoveReason (Destroyed by default), then applies weighting by duplicate definition count in graveyard.
- Include Transform as an explicit move reason for history classification.

## Risks / Trade-offs
- Reason coverage gaps can lead to Unknown reasons; mitigation: default to Unknown and update high-frequency move paths.
- Graveyard-only history changes behavior if cards leave graveyard in the future; this is acceptable under current "graveyard is stable" rule.

## Migration Plan
- Introduce move reason enum and APIs, then rewire existing move calls to pass reason.
- Update reanimate logic and tests.
- Remove DestroyedFollowerRecord and related tests.

## Open Questions
- Final list of move reasons required for card data conversion (Discard, Burn, Countdown, SpellPlayed, Transform, EffectDestroy, CombatDestroy, etc.).
