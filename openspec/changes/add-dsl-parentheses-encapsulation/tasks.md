## 1. Implementation
- [x] Add a ParenthesizedExpressionNode (or equivalent) to the DSL AST model.
- [x] Update the DSL parser to emit ParenthesizedExpressionNode for explicit parentheses.
- [x] Update DslController parse mapping to include ParenthesizedExpression nodes in DslAstNodeDto.
- [x] Update DSL evaluator/compiler to unwrap ParenthesizedExpressionNode during evaluation.
- [x] Map ParenthesizedExpression nodes to encapsulated UI nodes in src/components/dsl_editor/ast-mapping.js.
- [x] Add tests for parse API output (parentheses preserved) and UI mapping (encapsulated nodes restored).

## 2. Validation
- [ ] Run frontend tests covering DSL AST mapping.
- [ ] Run backend tests covering DSL parse + evaluator for ParenthesizedExpressionNode.
