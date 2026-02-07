## Context
The DSL currently supports arithmetic/logical operators and collection pipeline methods (query/orderBy/take), but cannot combine multiple collection sources in a single expression.

## Goals / Non-Goals
- Goals:
  - Provide difference/union/intersection operators for collection expressions in SourcePoolExpression and other DSL contexts.
  - Define type rules and deduplication behavior to keep runtime results stable.
  - Expose the operators in the DSL editor when a collection type is expected.
- Non-Goals:
  - No new pipeline methods beyond the existing query/orderBy/take.
  - No behavioral changes to existing operators.

## Decisions
- Operator forms:
  - Union uses `|`, intersection uses `&`, difference uses `-` (A minus B).
  - When operands are collections, the operators use set semantics; otherwise they keep existing numeric/boolean behavior.
- Type rules:
  - Both operands must be collections with compatible element types.
  - Result element type matches the left operand.
- Deduplication:
  - Use `InstanceId` when available; otherwise fall back to `Equals`.
- Precedence:
  - `&` has higher precedence than `|`.
  - `-` stays at additive precedence; when operands are collections it is treated as set difference.
 - Difference implementation detail:
   - Treat `A - B` as a collection difference when both operands are collections.
   - Keep the left operand iteration order; filter out elements that appear in `B`.
   - Equality should prefer `InstanceId` (if present) and fall back to `Equals`.
   - If either operand is not a collection, the operator keeps its numeric behavior and the type checker must emit an error for set usage.

## Risks / Trade-offs
- Operator overloading can be ambiguous (`-` for numeric vs set difference).
  - Mitigation: enforce collection types in type checking and emit errors otherwise.

## Migration Plan
- This is additive and does not alter existing expressions.
- Implement parser/evaluator/editor support and validate with tests.

## Open Questions
- Should we also support unary complement (NOT) for collections? Current design only supports binary difference `A - B`.
- When elements lack `InstanceId`, do we need an explicit key selector for deduplication?
