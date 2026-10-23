## Context

The current `distinct` behavior was introduced as a scalar-key operation: value types and strings are valid, while entity/object keys are rejected. That works for fields like cost and class but breaks down for list-valued keys, especially `Tribe`.

The newer tribe system adds three constraints:

1. `Tribe` is still stored and exposed as `List<string>`.
2. `MatchAll` is a special tribe that expands through `ITribeSemantics`, not a literal equality-only marker.
3. Pocket Galaxy and similar card definitions must remain configuration-driven, so the correct backend logic must be reachable from the existing DSL `distinct` pipeline stage.

Separately, the user clarified the desired list-key behavior:

- each list key should be expanded into single elements
- deduplication should happen on those expanded single elements
- each source element may still be retained at most once
- the result should retain as many source elements as possible

This is not the same as "keep the first element and occupy all list entries forever". It is a maximum-cardinality assignment problem.

## Goals / Non-Goals

**Goals**
- Keep `distinct` as the only user-visible deduplication operator.
- Preserve existing scalar-key distinct semantics and order expectations.
- Add well-defined list-key distinct semantics that maximize retained source elements.
- Make `distinct(self.Tribe)` and `distinct(self.LeaveTargetSnapshot.Tribe)` automatically use tribe semantic expansion, including `MatchAll`.
- Keep execution deterministic when multiple maximum-cardinality solutions exist.

**Non-Goals**
- Add a new DSL method such as `tribeDistinct`.
- Expose matched list element metadata back to card definitions.
- Generalize this change into arbitrary weighted optimization or user-selectable matching strategies.
- Change how `Tribe` is stored or surfaced in DTOs.

## Decisions

### Decision: `distinct` keeps one syntax but has two execution modes
- Rationale: user habit and configuration compatibility require a single deduplication entrypoint.
- Approach:
  - scalar keys keep existing stable distinct semantics
  - list keys use expanded-element maximum-cardinality deduplication
- Alternatives considered:
  - add a new list-only or tribe-only DSL method: rejected because card definitions would not naturally route into the new backend logic

### Decision: list-key distinct uses maximum-cardinality matching, not greedy first-fit
- Rationale: greedy occupancy fails the user’s expected behavior for cases like:
  - `[士兵, 海洋]`
  - `[海洋, 妖精]`
  - `[妖精, 士兵]`
  A greedy first-fit keeps one card; maximum matching keeps three.
- Approach:
  - materialize source items in original order
  - expand each list-valued key into a de-duplicated ordered set of atomic keys
  - build a bipartite graph between source items and expanded atomic keys
  - compute a maximum-cardinality matching
  - return matched source items only once each
- Alternatives considered:
  - occupy all expanded keys for the first retained item: simpler, but produces lower-cardinality results than required

### Decision: tribe list expansion is a specialized expander within generic list distinct
- Rationale: list distinct should be reusable for future list-valued keys, but `Tribe` needs semantic expansion through `ITribeSemantics`.
- Approach:
  - generic list expander: trim, remove empty values, deduplicate while preserving order
  - tribe list expander: call `ITribeSemantics.Expand(...)` first, then preserve stable order where possible
  - identify tribe keys during expression compilation rather than blindly treating every `List<string>` as tribe
- Alternatives considered:
  - special-case all `IEnumerable<string>` as tribe: rejected because other string lists may exist and should not receive `MatchAll` expansion

### Decision: tie-breaks must be deterministic
- Rationale: maximum matching may have multiple optimal solutions; game behavior and tests need a stable outcome.
- Approach:
  - first maximize number of retained items
  - among equivalent solutions, prefer earlier source items
  - for a single item with multiple feasible atomic keys, prefer earlier expanded-key order
  - implement the matcher with stable traversal order so equivalent inputs produce identical outputs
- Alternatives considered:
  - rely on incidental dictionary/list iteration order: rejected as too brittle

### Decision: QueryConfig distinct follows the same scalar-vs-list semantics as DSL
- Rationale: the system already treats DSL and QueryConfig pipelines as equivalent capabilities; distinct semantics must not diverge.
- Approach:
  - update `QueryExecutor` distinct handling to use the same branching and list expansion rules as DSL execution
- Alternatives considered:
  - keep QueryConfig scalar-only: rejected because it would make editor and backend behavior inconsistent

## Execution Model

### Scalar distinct
- Input: any non-entity value type or string
- Behavior: preserve first occurrence order; remove later equal keys
- Compatibility: unchanged from current behavior

### List distinct
- Input: any list-like key expression resolved to a collection of atomic values
- Behavior:
  1. materialize source items
  2. expand each item’s list key into stable atomic keys
  3. discard empty expansions
  4. solve maximum-cardinality matching between items and atomic keys
  5. return matched source items in original source order

### Tribe list distinct
- Input: a key expression identified as `Tribe`
- Behavior:
  1. call `ITribeSemantics.Expand(...)`
  2. convert `MatchAll` into current known normal tribes
  3. use the same list-distinct matching flow as above

## Implementation Notes

- `ExpressionTreeBuilder` should classify distinct key semantics during compilation:
  - scalar
  - generic list
  - tribe list
- `DslQueryOperations` should receive enough metadata to avoid guessing tribe semantics from runtime values alone.
- A simple DFS-based augmenting-path matcher is sufficient for current expected sizes and easier to reason about than a more complex Hopcroft-Karp implementation.
- Key expansion should be materialized once per source item to avoid repeated selector evaluation during matching.

## Risks / Trade-offs

- `distinct` becomes less trivial to explain because list keys no longer behave like scalar equality keys.
- Maximum matching is more computationally expensive than greedy distinct, but expected candidate counts are small enough that correctness is the priority.
- Tribe key classification adds compiler/runtime plumbing complexity, but avoids accidentally applying `MatchAll` semantics to unrelated string lists.
- Deterministic matching rules may keep earlier elements over later ones in cases where users expect a different optimal subset; this is acceptable as long as the rule is documented and stable.

## Migration Plan

1. Extend OpenSpec and implementation contract for `dsl-query` distinct semantics.
2. Update DSL execution path to classify scalar vs generic-list vs tribe-list keys.
3. Update QueryConfig distinct execution to share the same branching and matcher behavior.
4. Update DSL editor help text and validation copy so users understand list-key distinct behavior.
5. Add focused scalar/list/tribe regression tests, including `MatchAll` and event-history `Tribe` scenarios.

## Open Questions

- Whether the compiler should expose an explicit internal `DistinctKeySemantics` enum or infer through a richer selector descriptor object.
- Whether future non-string list keys need any normalization hooks beyond ordered atomic expansion.
