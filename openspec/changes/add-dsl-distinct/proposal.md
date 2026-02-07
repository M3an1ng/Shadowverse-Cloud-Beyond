# Change: Add Distinct Query Pipeline Operation

## Why
The query DSL and QueryConfig pipelines need a deterministic way to deduplicate collections by a caller-defined key expression while keeping key selection constrained to non-entity types for predictable behavior.

## What Changes
- Add a `distinct(keyExpr)` query pipeline method in the DSL with a required key expression.
- Enforce that `keyExpr` evaluates to non-entity types (value types or string); entity keys are rejected at runtime.
- Add a `DistinctOperation` to QueryConfig pipelines with a key selector and the same validation rule.
- Expose the Distinct stage in the DSL editor pipeline UI and restrict key selection to non-entity types.
- Add coverage for the new operation in backend and editor behaviors.

## Impact
- Affected specs: dsl-query (new).
- Affected code: Server/Domain/Abilities/DSL/Parser/Parser.cs, Server/Domain/Abilities/DSL/AST/QueryPipelineNodes.cs, Server/Domain/Abilities/DSL/Evaluation/ExpressionTreeBuilder.cs, Server/Domain/Abilities/DSL/Evaluation/DslQueryOperations.cs, Server/Domain/Query/QueryOperations.cs, Server/Domain/Query/QueryExecutor.cs, src/components/dsl_editor/*.
