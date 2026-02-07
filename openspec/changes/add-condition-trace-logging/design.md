## Context
We need richer diagnostics for condition evaluation failures without paying the cost of re-running expressions. The solution should collect trace values during a single evaluation and be controlled from the devtools UI.

## Goals / Non-Goals
- Goals:
  - Collect intermediate scalar values (string/number/bool/enum) during condition evaluation.
  - Keep overhead minimal when tracing is disabled.
  - Provide a devtools toggle to enable/disable tracing per session.
- Non-Goals:
  - Full serialization of complex entities or collections.
  - Always-on tracing in production.

## Decisions
- Decision: Add a trace collector interface used by the DSL evaluator/compiler to record node values.
  - Reason: Enables single-pass tracing without re-evaluation.
- Decision: Only materialize scalar values; summarize complex values by type and optional count.
  - Reason: Avoid heavy serialization and large logs.
- Decision: Tracing is disabled by default and enabled via devtools UI toggle.
  - Reason: Preserve runtime performance unless explicitly requested.

## Risks / Trade-offs
- Added evaluator instrumentation may introduce a small constant overhead; mitigate with fast checks when tracing is off.
- Trace output could still be noisy for complex expressions; mitigate with truncation and type summaries.

## Migration Plan
- Add tracing components behind a feature toggle.
- Roll out devtools toggle and default it to off.

## Open Questions
- Resolved: store toggle state in server memory per user session (no persistence).
- Resolved: tracing applies only to ability condition evaluation.
