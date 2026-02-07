## 1. Implementation
- [ ] 1.1 Extend DSL parser/AST to represent `.query`, `.orderBy`, `.take` as collection operations.
- [ ] 1.2 Implement collection operations in ExpressionTreeBuilder (Where/OrderBy/Take, including random strategy).
- [ ] 1.3 Expose AbilityTargets for all DSL contexts via TargetSelectionContext in DslCompiler.
- [ ] 1.4 Route ConditionEvaluator and ValueSourceResolver through TSC-aware compile APIs (empty TSC when not resolving targets).
- [ ] 1.5 Update QueryExecutor/QueryDslParser to delegate to unified DSL path or convert legacy query DSL to the new expression form.
- [ ] 1.6 Add tests covering query operations in targets, conditions, and effect parameters.
- [ ] 1.7 Update DSL docs/examples in editor metadata if needed.
