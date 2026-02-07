# Change: Event parameter MemberAccess in DSL

## Why
Event parameters are currently accessed via `eventCard/eventInt/...` helper functions because `context.triggeringEvent` is typed as `IGameEvent`. This breaks the “DSL round-trip via UI” rule: users cannot build or edit `eventCard(...).Type` as a property chain in the editor. We need a MemberAccess path that maps event parameters into `context.triggeringEvent.<Param>` so DSL can be reconstructed through the UI.

## What Changes
- DSL evaluation/compilation resolves `context.triggeringEvent.<Param>` via `EventParameterCache`, returning a typed value to allow further MemberAccess chaining.
- DSL editor exposes event parameters as properties when the user navigates into `context.triggeringEvent`, emitting MemberAccess chains instead of `eventCard/eventInt` helpers.
- Existing `eventCard/eventInt/eventString/eventBool/eventGuid` remain supported for backward compatibility; when possible the UI should map them into MemberAccess chains.

## Impact
- Impacted specs: dsl-event-memberaccess
- Impacted code: Server/Domain/Abilities/DSL/Evaluation/ExpressionTreeBuilder.cs, Server/Domain/Events/Metadata/EventParameterCache.cs (access helpers), src/components/dsl_editor/options-provider.js, src/components/dsl_editor/ast-mapping.js, src/components/DslEditor.js
