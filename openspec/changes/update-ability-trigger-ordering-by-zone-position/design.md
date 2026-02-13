## Context
Ability trigger ordering currently prioritizes event role and zone/ownership priority, but does not apply a strict in-zone position rule. This allows same-zone triggers to fall back to registration timing, which is not a gameplay-facing ordering model.

## Goals
- Enforce deterministic same-zone trigger ordering by battlefield position.
- Keep compatibility with existing cross-zone and event-role priorities.
- Provide stable tie-breakers to guarantee reproducible ordering.
- Make ordering logic explicit, centralized, and testable.

## Non-Goals
- No redesign of the transaction loop (`eventStagingArea` / effect queue flow).
- No new stack system or player-priority response window.
- No changes to effect semantics beyond trigger ordering.

## Decisions
- Decision: Use a centralized ordering policy component for all triggered ability sorting.
  - Rationale: Avoids duplicated or drifting ordering rules.

- Decision: Compute a lexicographic trigger order key:
  1) special role priority (attacker/defender/event source),
  2) zone priority (existing behavior),
  3) zone position (`ZoneIndex` ascending),
  4) zone enter sequence (`ZoneEnterSequence` ascending),
  5) ability order on card,
  6) runtime registration order.
  - Rationale: Preserves current intent while adding strict in-zone determinism.

- Decision: `ZoneIndex` is authoritative for current position; `ZoneEnterSequence` is authoritative for stable tie-break among equal/unknown positions.
  - Rationale: Position models board order; entry sequence preserves deterministic historical fallback.

- Decision: Entering a zone assigns a new monotonic `ZoneEnterSequence`; leaving does not reset global counter.
  - Rationale: Re-entry should be treated as a new entry event for deterministic ordering.

- Decision: Existing special cases (attacker/defender/source-card precedence) remain ahead of generic zone ordering.
  - Rationale: Maintains compatibility with established combat/event expectations.

## Risks / Trade-offs
- Risk: Existing behavior-dependent tests may fail due to newly deterministic order.
  - Mitigation: Update tests to explicit order assertions and add migration notes.

- Risk: Partial direct list mutations (bypassing zone APIs) can desynchronize `ZoneIndex`/entry sequence.
  - Mitigation: Consolidate zone mutations and add guard assertions/tests.

## Migration Plan
1. Introduce ordering policy and non-invasive logging.
2. Add `ZoneEnterSequence` and populate on zone-entry paths.
3. Switch ability sorting to the new policy.
4. Update and extend tests for deterministic ordering.
5. Remove temporary diagnostics once stable.

## Open Questions
- Should any zone types (for example `Emblem`) opt out of `ZoneIndex` ordering?
- Should we expose ordering-key traces in debug DTOs or keep logs only?
