# Change: EventHistory DSL access via GameContext

## Why
Designers need a general way to compute match and turn-based statistics without adding bespoke counters.

## What Changes
- Add a read-only EventHistory reader on GameContext for DSL evaluation.
- Introduce an EventHistory query scope model (match, current-turn, last-turn, recent) with full event access.
- Add a DSL function `events(scope, filterExpr?, limit?)` to retrieve event lists for counting and aggregation; limit is optional and has no default cap.

## Impact
- Affected specs: event-history-dsl
- Affected code: Server/Domain/Events, Server/Domain/Events/Services, Server/Domain/Events/GameContext.cs, Server/Domain/Abilities/DSL/Functions
