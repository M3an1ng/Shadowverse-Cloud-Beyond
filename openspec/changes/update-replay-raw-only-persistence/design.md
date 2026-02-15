## Context
Current replay writes both `ClientDtos` and `RawEventJson`. Storage analysis shows DTO payload is the primary size driver. We need a low-risk path that preserves real-time game behavior while shrinking persistent replay footprint.

## Goals / Non-Goals
- Goals:
  - Make raw-event facts the persisted replay source of truth.
  - Eliminate DTO persistence by default.
  - Preserve `CatchUp`/`Seek` functionality.
  - Maintain backward compatibility with legacy replay rows.
- Non-Goals:
  - Replacing live SignalR event broadcasting.
  - Refactoring all domain events or commit semantics.
  - Immediate schema deletion of `DtosJson` column.

## Decisions
- Decision: Use raw-only persistence mode by default.
  - Journal keeps `RawEventJson` + stable metadata (`Seq`, `EventId`, `EventType`, timestamps, source/targets).
  - `ClientDtos` are not stored unless explicitly enabled for diagnostics.

- Decision: Replay read path becomes projection-first when DTOs are absent.
  - `CatchUp`/`Seek` read records in `Seq` order.
  - If `ClientDtos` exists (legacy rows), existing path may still be used.
  - Otherwise, deserialize `RawEventJson` and project to client DTO stream.

- Decision: Replay projection must be apply-only, never re-commit.
  - Replay reconstruction must not call `ICommittableEvent.Commit()`.
  - This prevents duplicate child-event generation (for example, `TurnStart` generating `Draw` again).

- Decision: Keep progressive rollout with feature flags.
  - Add explicit options for persistence mode and projection fallback.
  - Start with local/QA verification, then production rollout.

## Risks / Trade-offs
- Risk: raw->client projection gaps for long-tail event types.
  - Mitigation: fallback path, typed coverage checklist, parity tests.
- Risk: behavior drift between live DTO and replay DTO projections.
  - Mitigation: golden replay parity tests on recorded matches.

## Migration Plan
1. Add replay persistence mode option and default to raw-only.
2. Update writer to skip `ClientDtos` persistence.
3. Update `CatchUp`/`Seek` to support raw projection when DTOs missing.
4. Validate with parity tests and representative recorded matches.
5. Roll out and monitor replay read error metrics.

## Open Questions
- Do we keep a temporary per-environment override to persist DTOs for debugging?
- Should old DTO payload be backfilled/removed, or retained until retention cleanup naturally removes it?
