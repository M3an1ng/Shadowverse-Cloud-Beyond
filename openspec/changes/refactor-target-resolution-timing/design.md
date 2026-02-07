## Context
Ability target resolution currently happens before effects execute, which can desync targets from the latest board state.
Primary abilities tied to IPlayerActionEvent must remain pre-resolved to support cancellable player choices.

## Goals / Non-Goals
- Goals:
  - Resolve targets at effect execution time for non-player-action triggers.
  - Preserve pre-resolution for player-action primary abilities.
  - Keep behavior consistent across trigger paths.
- Non-Goals:
  - No new UI choice flows.
  - No changes to existing DSL semantics.

## Decisions
- Decision: Defer target resolution to effect execution time by default.
- Decision: Keep AnalyzePrimaryAbilitiesAsync pre-resolution for IPlayerActionEvent primary abilities.
- Alternative: Per-ability resolveTiming flags. Not chosen for this change.

## Risks / Trade-offs
- Risk: Some abilities that previously relied on early resolution may behave differently.
  - Mitigation: Limit the change to non-IPlayerActionEvent paths.
- Risk: Execution-time resolution must never prompt for player choice.
  - Mitigation: Player-choice targets remain in pre-resolution path.

## Migration Plan
- Implement execution-time resolution for non-player-action abilities.
- Regression test PlayCard/Evolve/SuperEvolve/Act interactions.
- Verify summon-then-buff chains resolve new targets.

## Open Questions
- None. IPlayerActionEvent stays as-is; other abilities defer resolution.
