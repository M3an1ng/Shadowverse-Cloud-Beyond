# Change: Pipeline Inline Autocomplete Tooltips

## Why
Pipeline stage editing is moving to a lightweight formula input, but users still need guided discovery of valid nodes and operators. Tooltip-style suggestions should reuse the existing DSL node recommendation logic so the experience stays consistent while remaining lightweight.

## What Changes
- Add tooltip autocomplete to the pipeline stage lightweight input.
- Suggestions reuse existing DSL node recommendation logic (properties, operators, functions, event fields).
- Selecting a suggestion inserts the corresponding DSL token into the input.

## Impact
- Affected specs: dsl-editor
- Affected code: src/components/dsl_editor/pipeline-editor.js, src/components/dsl_editor/ui-pipeline.js, src/components/dsl_editor/options-provider.js, src/styles/DslEditor.css
