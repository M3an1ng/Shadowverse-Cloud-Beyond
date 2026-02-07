## 1. Implementation
- [x] Update CountFunction to accept exactly one argument and remove property-based counting.
- [x] Adjust metadata/parameter definitions for count to match unary signature.
- [x] Update any usages/tests expecting count(collection, property).
- [x] Decide whether to keep Count/IsEmpty pseudo-properties as compatibility sugar.
- [x] Update Sum/Max/Min functions to accept exactly one argument (numeric collection only).
- [x] Remove legacy aggregate signatures (property-based and multi-arg variants) for Sum/Max/Min.
- [x] Update DSL function metadata generation to preserve collection inner types for function parameters.
- [x] Update frontend function filtering so sum/max/min only show for numeric collections.

## 2. Tests
- [x] Update CountFunction tests to unary only.
- [ ] Add regression test for counterName usage if applicable.
- [x] Update Sum/Max/Min function tests for unary numeric collection only.
- [x] Add rejection tests for legacy signatures (property-based and multi-arg).
- [x] Add UI/DSL validation tests for numeric-collection-only filtering.

## 3. Docs
- [x] Update DSL docs/examples to use count(x) unary signature.
- [x] Update DSL docs/examples for sum/max/min unary numeric list usage.
