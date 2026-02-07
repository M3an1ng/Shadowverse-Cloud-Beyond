## 1. Implementation
- [x] 1.1 Update DSL lexer/parser: add `|` and `&` tokens plus collection operator parsing and precedence.
- [x] 1.2 Update AST/type inference: collection operators yield collection types and validate element compatibility.
- [x] 1.3 Update ExpressionTreeBuilder/compiler: map set operators to Union/Intersect/Except with dedup rules.
- [x] 1.4 Update DSL editor options/serialization to expose set operators for collection types.
- [x] 1.5 Add tests for parsing/type checking/execution of set operators and query entry points.
