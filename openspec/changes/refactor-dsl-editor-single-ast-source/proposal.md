# Change: Refactor DSL Editor Single AST Source

## Why
- The editor currently allows `astRoot` and `workspaceAST` to diverge, which causes visible edits to fail to persist when saving.
- A single source of truth reduces state drift and simplifies debugging.

## What Changes
- Frontend DSL editor uses `workspaceAST` as the only mutable source of truth for edit/render/save flows.
- `astRoot` becomes an alias/derived view of `workspaceAST` and no longer mutates independently.
- Update edit entry points, undo/unnest flows, and save paths to enforce the invariant.
- Add guardrails (logging/tests) to detect unintended divergence.

## Impact
- Affected specs: `dsl-editor`
- Affected code: `src/components/DslEditor.js`, `src/components/dsl_editor/*`, `src/dslValidation.js`
