## 1. Implementation
- [ ] Update PostProcessingOperation config to accept a projection expression/value config (self-based).
- [ ] Update QueryExecutor post-processing logic to apply projection and return projected list.
- [ ] Remove/retire ToCardDefinitionDto/ToCardInstanceDto handling paths (or gate behind migration if kept).
- [ ] Update DSL pipeline editor to enable and configure post-process stage (expression input).
- [ ] Add SelectOperation to query pipeline model and executor.
- [ ] Update DSL parser/DTOs to emit select pipeline stage where applicable.
- [ ] Update DSL pipeline editor to support select stage creation/editing.
- [ ] Update serialization/DTOs as needed for new post-process config.

## 2. Tests
- [ ] Add backend tests for post-processing projection (CardInstance -> Int list).
- [ ] Add frontend test covering pipeline post-process stage label/config mapping.
- [ ] Add backend test for select stage projection.
- [ ] Add frontend test for select stage UI/AST mapping.

## 3. Docs
- [ ] Update pipeline documentation / UI hints to reflect new post-process behavior.
