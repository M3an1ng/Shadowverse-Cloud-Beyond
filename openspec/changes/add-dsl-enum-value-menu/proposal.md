# Change: Add DSL Enum Value Menu

## Why
- Enum-typed DSL fields currently require manual string input, which is error-prone and inconsistent.
- Enum values are already available in metadata and should be selectable in the editor UI.

## What Changes
- Add enum value menu entries in the DSL editor when the expected type is an enum.
- Populate options from DSL metadata (enum types); hide manual input when options exist and only allow it as a fallback when metadata is missing.
- Insert the selected enum value as a typed literal in the expression.

## Impact
- Affected specs: `dsl-editor`
- Affected code: `src/components/DslEditor.js`, `src/components/dsl_editor/operators.js`, `src/components/dsl_editor/options-provider.js`, `src/components/dsl_editor/ui-menus.js`
