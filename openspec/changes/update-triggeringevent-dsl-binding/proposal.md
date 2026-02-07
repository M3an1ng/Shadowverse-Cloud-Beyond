# Change: TriggeringEvent DSL binding and parameter exposure

## Why
The DSL editor currently exposes TriggeringEvent parameters in the global menu and still shows GameEventBase base fields. This causes confusion and makes it easy to use the wrong data. We need to bind parameter exposure to GameContext -> TriggeringEvent and filter by event type.

## What Changes
- Bind TriggeringEvent parameter exposure to the GameContext -> TriggeringEvent entry.
- Only show accessible parameters for the selected event type.
- Do not expose GameEventBase base parameters in the editor.
- TriggeringEvent parameters are not listed as global data source options.

## Impact
- Impacted specs: dsl-event-params
- Impacted code: Server/Services/EventMetadataService.cs, Server/Services/DslMetadataService.cs, src/components/dsl_editor/options-provider.js, src/components/DslEditor.js
