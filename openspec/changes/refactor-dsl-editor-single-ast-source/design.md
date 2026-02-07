## Context
The DSL editor currently maintains both `astRoot` and `workspaceAST`, which can diverge during edit, unnest, and save flows.

## Goals / Non-Goals
- Goals:
  - Single mutable AST for edit/render/save flows.
  - `astRoot` becomes a derived view of the workspace AST.
  - Avoid reintroducing removed nesting on save.
- Non-Goals:
  - Change backend DSL parsing rules.
  - Redesign UI layout or interaction beyond AST flow.

## Decisions
- Decision: Use `workspaceAST` as the sole mutable AST source and derive `astRoot` from it.
- Decision: Normalize save/ascend flows to accept an empty `editingPath` by using the workspace AST.

## Alternatives considered
- Keep dual ASTs with sync steps after every edit (more complexity, easy drift).
- Reparse from DSL text on every save (risk of losing UI-only state, slower).

## Risks / Trade-offs
- Risk: Some views might rely on stale references; mitigate with guards/logs when divergence is detected.
- Trade-off: Requires refactoring multiple entry points to stop mutating `astRoot` directly.

## Migration Plan
- Refactor entry points to use the single AST source.
- Add guardrails/logging and update tests.

## Open Questions
- Which existing UI flows still mutate `astRoot` directly after refactor?
- Do we need additional invariants for `editingPath` when entering main edit mode?
