# Design: Parentheses-aware encapsulation

## Goals
- Preserve explicit parentheses in the DSL parse API so the UI can restore encapsulated sub-expressions.
- Keep runtime semantics unchanged by unwrapping grouped expressions during evaluation.
 - Avoid any automatic normalization or insertion of parentheses beyond what the user saved.

## Approach
- Introduce ParenthesizedExpressionNode with a single Inner expression.
- Parser: when encountering parentheses around an expression, return a ParenthesizedExpressionNode instead of dropping the grouping.
- Parse API: include NodeType = ParenthesizedExpression and Inner in DslAstNodeDto.
- UI mapping: convert ParenthesizedExpression to a ValueNode with metadata.type = encapsulated and metadata.innerAST = mapped child.
- Evaluation: when evaluating or compiling, unwrap ParenthesizedExpressionNode to Inner.

## Compatibility
- Existing clients that do not recognize ParenthesizedExpressionNode will ignore it; the updated UI will handle it explicitly.
- No change to expression semantics; grouping is UI-only metadata.
 - Parentheses are only restored when present in the saved DSL string.
