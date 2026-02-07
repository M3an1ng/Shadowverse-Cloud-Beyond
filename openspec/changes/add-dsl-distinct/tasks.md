## 1. Implementation
- [ ] 1.1 Add DSL parser + AST support for `distinct(keyExpr)` in query pipelines.
- [ ] 1.2 Implement distinct evaluation in ExpressionTreeBuilder and DslQueryOperations, including runtime key type validation.
- [ ] 1.3 Add QueryConfig DistinctOperation and execute it in QueryExecutor with the same validation rule.
- [ ] 1.4 Update DSL editor pipeline UI to add Distinct stage and restrict key expression to non-entity types.
- [ ] 1.5 Add backend/unit tests for distinct semantics and validation errors.
