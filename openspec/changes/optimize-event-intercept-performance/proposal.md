# Change: Optimize event intercept performance

## Why
RewriteEvent/ReplaceEvent currently rely on reflection and repeated parameter scanning. When intercepts are used frequently (e.g., global damage modifiers), this overhead can become noticeable compared to standard effect execution.

## What Changes
- Add cached metadata and binders for event rewrites and replacements.
- Precompute parameter name mappings and value coercers for intercept inputs.
- Reuse cached constructor delegates to avoid repeated reflection scans.
- Centralize event parameter metadata/coercers so other systems (event metadata, DSL access, effect parameter binding) can reuse the same cache.
- Preserve existing behavior with a safe fallback to the current reflection path.

## Impact
- Affected specs: event-intercept-performance
- Affected code: GameActionTransactionService, AbilityManager (trigger filtering), event metadata resolution, Replace/Rewrite intercept handling
