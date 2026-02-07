## Context
CardQuery editing is currently expressed as chained DSL text (`.query/.orderBy/.take`). The DSL editor can parse and display the chain, but it is difficult to edit as a structured pipeline, and stage-level type hints are not explicit.

## Goals / Non-Goals
- Goals:
  - Provide a pipeline editor UI for CardQuery/collection nodes.
  - Use a structured pipeline AST to support add/remove/reorder and stage parameter editing.
  - Preserve round-trip fidelity with CardQuery DSL syntax.
  - Provide stage-level type context for validation and capsule hints.
- Non-Goals:
  - Rewrite the underlying CardQuery execution semantics.
  - Introduce a new query language beyond the existing DSL syntax.

## Decisions
- Decision: Introduce a Pipeline AST node with `stages[]` for query chains.
  - Reason: Enables stable UI editing and type-aware guidance without relying on text-only manipulation.
- Decision: Parse `.query/.orderBy/.take` chains into stages; serialize stages back to the same DSL syntax.
  - Reason: Preserves compatibility with existing DSL storage and backend parsing.
- Decision: Represent unsupported segments as raw stages.
  - Reason: Avoids data loss and enables gradual support for new stage kinds.
- Decision: Stage output types define the expected type for the next stage.
  - Reason: Keeps type hints local and consistent with pipeline flow.

## Stage Types (Aligned with Backend)
- Source: maps to `QueryConfig.SourcePoolExpression` (initial pool DSL).
- Filter: maps to `FilterOperation` with `Filters[]` (`FilterConfig` uses `ValueSource`, `TargetValue`, `Operator`).
- Sort: maps to `SortOperation` with `Ordering[]` (`OrderingConfig` uses `ValueSource`, `Direction`).
- Select: maps to `SelectionOperation` (`SelectionStrategy`, `CountSource`) and covers `.take(n)` as `First`.
- PostProcess: maps to `PostProcessingOperation` (`PostProcessingType`, optional `Parameters`).
- Raw: preserves any unsupported/unknown DSL segment as an opaque stage.

## Stage Schema and DSL Mapping
- Source
  - Fields: `expression` (DSL string).
  - Backend: `QueryConfig.SourcePoolExpression`.
  - UI: editable DSL input; provides initial list type for subsequent stages.
- Filter
  - Fields: `filters[]` of `{ predicateDsl }` (current backend only uses `ValueSource.Formula` as predicate).
  - Backend: `FilterOperation.Filters[]` with `ValueSource = FormulaValueConfig{Formula}`; `TargetValue/Operator` reserved.
  - DSL: `.query(<predicate>)` maps to a single filter predicate; multiple predicates are ANDed by executor.
- Sort
  - Fields: `ordering[]` of `{ keyDsl, direction }`.
  - Backend: `SortOperation.Ordering[]` with `ValueSource = FormulaValueConfig{Formula}`.
  - DSL: `.orderBy(expr)` / `.orderBy(expr desc, expr2 asc)` maps to ordering list.
- Select
  - Fields: `strategy` (`First|Last|Random`), `count` (ValueConfig or numeric literal).
  - Backend: `SelectionOperation` (`Strategy`, `CountSource`).
  - DSL: `.take(n)` maps to `strategy=First` and `count=FormulaValueConfig{Formula=n}`.
  - Constraint: `Last/Random` require a new DSL syntax; until added, serialize as Raw or block in UI.
- PostProcess
  - Fields: `type` (`None|ToCardDefinitionDto|ToCardInstanceDto`), optional `parameters`.
  - Backend: `PostProcessingOperation`.
  - DSL: currently no parser support; keep disabled or serialize as Raw until grammar is extended.
- Raw
  - Fields: `rawText`.
  - Behavior: displayed but not edited; preserves original DSL segment for round-trip.

## Risks / Trade-offs
- Added complexity in AST mapping and serialization paths.
- Partial support for stage kinds may lead to raw stage fallbacks.

## Migration Plan
- No data migration required; existing DSL strings remain valid.
- UI will parse supported chains into pipeline stages; unsupported segments stay as raw stages.

## Open Questions
- Which stage kinds are in scope for the first iteration (filter/order/take/select/aggregate)?
- How to represent advanced query clauses (multi-key ordering, projections) in a minimal UI?
