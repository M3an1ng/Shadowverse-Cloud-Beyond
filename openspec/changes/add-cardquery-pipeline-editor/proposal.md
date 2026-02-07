# Change: Add CardQuery Pipeline Editor

## Why
CardQuery is currently edited as chained DSL text (`.query/.orderBy/.take`), which is hard to visualize, reorder, or type-check during intermediate steps. A structured pipeline editor is required for reliable UI editing and type-aware guidance.

## What Changes
- Introduce a pipeline editor UI for collection/query nodes inside the DSL editor.
- Normalize query chains into a pipeline AST with explicit stages (filter, order, take, select, aggregate).
- Support round-trip conversion between pipeline AST and CardQuery DSL syntax.
- Provide stage-level type context and validation; preserve raw fragments when parsing fails.

## Impact
- Specs: dsl-editor
- Frontend: DSL editor UI, pipeline stage components, type hint display
- Backend: CardQuery DSL parser/serializer integration (if shared), tests
- Tests: UI interaction and round-trip parsing/serialization
