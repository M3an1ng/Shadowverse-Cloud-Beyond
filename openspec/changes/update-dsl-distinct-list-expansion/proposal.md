# Change: Update DSL Distinct for Expanded List Keys

## Why

`distinct(keyExpr)` is already the user-facing deduplication entrypoint in the DSL editor and QueryConfig pipeline UI. The game is configuration-driven, so any backend-only tribe-distinct helper that is not reachable through `distinct` will never be exercised by card definitions such as Pocket Galaxy.

The current `distinct` contract only supports scalar keys and rejects collection keys. That is no longer sufficient:

- card and snapshot `Tribe` values are lists, and they now have runtime semantics including `MatchAll`
- other list-valued keys may also need deduplication in the future
- the desired user-facing behavior is still "use `distinct`", not "learn a different tribe-only function"

The system therefore needs to upgrade `distinct` itself so that list-valued keys are first expanded into single-element keys and then deduplicated in a way that preserves as many source elements as possible while still keeping each source element at most once.

## What Changes

- Keep `collection.distinct(keyExpr)` as the only DSL and QueryConfig deduplication entrypoint.
- Preserve existing scalar-key behavior for non-list keys.
- Extend `distinct` so list-valued keys use expanded-element deduplication instead of runtime validation failure.
- Define list-key distinct semantics as a maximum-cardinality matching problem:
  - each source element may be kept at most once
  - each expanded single-element key may be assigned to at most one source element
  - the executor should maximize the number of retained source elements
- Add special handling for `Tribe` list keys so they expand through `ITribeSemantics`, including `MatchAll` expansion to current known normal tribes.
- Keep frontend UX unchanged while updating editor help/validation to reflect the new scalar-vs-list distinct semantics.

## Capabilities

### Modified Capabilities
- `dsl-query`: `distinct` continues to support scalar keys and now supports list-valued keys using expanded maximum-cardinality deduplication, including tribe-semantic expansion for tribe lists.

### New Capabilities
- None.

## Impact

- Affected specs:
  - `dsl-query`
- Affected code:
  - `Server/Domain/Abilities/DSL/Evaluation/ExpressionTreeBuilder.cs`
  - `Server/Domain/Abilities/DSL/Evaluation/DslQueryOperations.cs`
  - `Server/Domain/Query/QueryExecutor.cs`
  - `Server/Domain/Abilities/DSL/Functions/*` if helper metadata or utilities are added
  - `Server/Services/Abstractions/ITribeSemantics.cs`
  - `Server/Services/TribeSemantics.cs`
  - `src/components/dsl_editor/*`
- Affected systems:
  - DSL query execution
  - QueryConfig execution
  - tribe-aware event-history selection such as Pocket Galaxy
