## Context
The current permission model relies on enums or ability preconditions, which cannot represent
conditional restrictions without complex ability combinations. This creates ambiguity and makes
UI feedback inconsistent.

## Goals / Non-Goals
- Goals:
  - Provide a parameterized restriction model for actions (Play/Attack/Evolve/Act).
  - Ensure permission checks are centralized and consistent across playability and action requests.
  - Provide actionable denial reasons to clients.
- Non-Goals:
  - Replacing existing effect/ability execution logic.
  - Introducing a new DSL syntax beyond existing condition DSL evaluation.

## Decisions
- Decision: Use buff/status instances to represent action restrictions with parameters.
- Decision: Centralize evaluation in an ActionPermissionService used by playability and request validation.
- Decision: Keep keywords as unconditional tags and avoid parameterized keyword instances.

## Risks / Trade-offs
- Buff-based restrictions introduce more moving parts; mitigate with a single evaluation entry point.
- Permission checks may add CPU cost; mitigate with simple predicates and short-circuiting.

## Migration Plan
- Introduce restriction model alongside existing checks.
- Route playability and action request validation through the permission service.
- Incrementally migrate existing "cannot act" logic into restriction buffs where applicable.

## Open Questions
- Which action types require immediate support (Play vs Attack/Evolve/Act)?
- Should restriction evaluation be cached per turn or per action request?
