# Change: Update DSL collection auto-boxing

## Why
Target selection and DSL editing frequently produce single-card expressions where a collection is expected. The backend already auto-boxes single values in some query sources; extending this behavior globally reduces false-negative validation and aligns runtime behavior with author intent.

## What Changes
- Treat non-collection values as singleton collections whenever a DSL evaluation context expects `IEnumerable`/`List<T>`.
- Explicitly exclude strings from auto-boxing to avoid treating them as character sequences.
- Add tests that cover collection operators, query sources, and collection-oriented DSL functions with single-item inputs.

## Impact
- Affected specs: dsl-operators
- Affected code: DSL evaluator/compiler, query execution, function evaluation, type enforcement
