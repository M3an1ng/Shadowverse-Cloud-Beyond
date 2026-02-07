# Change: Add DSL set operators (difference/union/intersection)

## Why
The current DSL cannot express collection-level union/intersection/difference, so target queries must rely on larger pools plus filters, which reduces clarity and reuse.

## What Changes
- Extend the DSL grammar and type system to support collection difference, union, and intersection operators.
- Extend DSL evaluation/compilation to map set operators to runtime collection operations.
- Expose collection operators in the DSL editor to keep visual authoring aligned with syntax.
- Add parsing/execution tests that cover the new operators.

## Impact
- Affected specs: dsl-operators
- Affected code: DSL Lexer/Parser/AST/ExpressionTreeBuilder, type checking, DSL editor options/serialization, tests
