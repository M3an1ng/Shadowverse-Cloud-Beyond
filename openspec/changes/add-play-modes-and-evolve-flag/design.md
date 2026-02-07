## Context
Enhance is being modeled as a play mode that may override the base card behavior when affordable. Super evolve currently triggers the same EvolveEvent as normal evolve, making it impossible to distinguish triggers.

## Goals / Non-Goals
- Goals:
  - Provide deterministic play mode selection with Enhance as a higher-priority mode.
  - Allow abilities to detect super evolve vs normal evolve.
  - Expose mode availability and selection to the frontend.
- Non-Goals:
  - Implement a general multi-resource cost system in this change.
  - Redesign the DSL grammar or the ability editor UI beyond required data fields.

## Decisions
- Decision: Compute play modes during playability checks and include them in PlayabilityResultDto.
  - Reason: Keeps UI consistent with backend rules and avoids late-stage cost conflicts.
- Decision: Add IsSuperEvolution to EvolveEvent rather than suppressing EvolveEvent during super evolve.
  - Reason: Preserves existing evolve triggers while allowing explicit branching.

## Risks / Trade-offs
- Mode selection logic must remain deterministic across backend checks and actual play to avoid desync.
- DTO changes require frontend updates to consume new mode information.

## Migration Plan
- Add new fields with backward-compatible defaults.
- Update UI to consume mode info; fall back to Base when missing.

## Open Questions
- Should Enhance be strictly a replacement-cost mode or an extra-cost mode?
- What standardized reasons should be returned when a mode is unavailable?
