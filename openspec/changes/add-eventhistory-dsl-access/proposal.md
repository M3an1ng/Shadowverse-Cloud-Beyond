# Change: Systematize Typed Event Access for Event Enumerables in DSL

## Why
Current editor narrowing based on expression inference (for example `self.EventType == "Destroy"`) is unstable and hard to scale. Designers need a deterministic, natural path to reach typed event parameters from any `IEnumerable<IGameEvent>`, including `context.eventHistory.events`.

## What Changes
- Keep `context.eventHistory.events` as the canonical full-history entry with no whitelist and no capped public view.
- Introduce a generic DSL function `byType(source, eventType)` that accepts any `IEnumerable<IGameEvent>` source and returns the filtered event collection.
- Reuse existing member-call lowering so `source.byType("Damage")` maps to `byType(source, "Damage")` without grammar changes.
- Keep the filtered result pipeline-compatible (`query/orderBy/take/distinct/select`) so users can write natural chains like:
- `context.eventHistory.events.byType("Damage").query(self.DamageAmount > 0)`
- Add deterministic frontend type narrowing:
- Extract `eventTypeHint` from `byType(<literal/enum>)`
- Propagate hint across pipeline scopes
- Use hint-driven metadata to expose `self` event parameters
- Provide fallback behavior when hint is unresolved: keep base `IGameEvent` fields only.
- Explicitly remove `countByType` and dictionary-style `ByType` from this change scope.

## Impact
- Affected specs: `event-history-dsl`
- Affected backend:
- `Server/Domain/Abilities/DSL/Functions/CoreFunctions/*`
- `Server/Domain/Abilities/DSL/Functions/FunctionRegistry.cs`
- `Server/Domain/Abilities/DSL/Evaluation/ExpressionTreeBuilder.cs` (compatibility verification)
- `Server/Domain/Events/EventHistoryReader.cs` (optional performance helper only, no DSL contract dependency)
- Affected frontend:
- `src/components/dsl_editor/options-provider.js`
- `src/components/dsl_editor/ast-editing.js`
- `src/components/dsl_editor/pipeline-editor.js`
- `src/components/dsl_editor/property-chain-resolver.js`
- `src/components/dsl_editor/ast-mapping.js`
- `src/components/dsl_editor/event-translation.js`
