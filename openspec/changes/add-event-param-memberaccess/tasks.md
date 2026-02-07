## 1. Implementation
- [x] 1.1 Backend: add a helper that resolves event parameters from an IGameEvent using EventParameterCache and returns a typed value.
- [x] 1.2 Backend: update ExpressionTreeBuilder to map MemberAccess on `context.triggeringEvent` to the event-parameter helper, enabling chained access.
- [x] 1.3 Frontend: expose event parameters as property options under `context.triggeringEvent` (emit MemberAccess chains).
- [x] 1.4 Frontend: map legacy `event*` function calls to MemberAccess nodes when possible (fallback to raw text otherwise).
- [x] 1.5 Tests: add round-trip coverage for `context.triggeringEvent.<Param>` and chained property access.
