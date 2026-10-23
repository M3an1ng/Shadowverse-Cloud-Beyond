## 1. Backend Distinct Semantics

- [x] 1.1 Update DSL distinct compilation to classify scalar keys, generic list keys, and tribe list keys.
- [x] 1.2 Extend DSL distinct execution so scalar keys keep current behavior while list keys use expanded-element maximum-cardinality matching.
- [x] 1.3 Integrate tribe-key distinct with `ITribeSemantics.Expand(...)`, including `MatchAll` expansion.

## 2. QueryConfig Parity

- [x] 2.1 Update QueryConfig distinct execution to share the same scalar-vs-list semantics as DSL distinct.
- [x] 2.2 Ensure unsupported entity/object keys still fail fast with clear validation errors.

## 3. Frontend DSL UX

- [x] 3.1 Keep the existing `distinct` pipeline UI unchanged as the entrypoint for deduplication.
- [x] 3.2 Update DSL editor help text, validation hints, or stage descriptions to explain list-key expanded distinct behavior.

## 4. Validation

- [x] 4.1 Add backend tests proving scalar distinct behavior remains unchanged.
- [x] 4.2 Add backend tests for generic list-key distinct using maximum-cardinality matching.
- [x] 4.3 Add backend tests for tribe-key distinct, including `MatchAll` expansion and the three-card cycle example.
- [x] 4.4 Add QueryConfig parity tests covering the same scalar/list/tribe cases.
- [x] 4.5 Run `openspec validate update-dsl-distinct-list-expansion --strict`.
