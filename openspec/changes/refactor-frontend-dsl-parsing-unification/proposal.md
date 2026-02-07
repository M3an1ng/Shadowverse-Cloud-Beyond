# Change: Unify frontend DSL parsing pipeline

## Why
The frontend has multiple DSL parsing paths (editor render, validation, and local parsing). This causes inconsistent type inference and available fields for the same expression, which hurts stability and predictability.

## What Changes
- Frontend DSL editor render, validation, and local parsing use a single AST construction and type inference rule set.
- Reduce or replace local heuristic parsing in favor of shared, stable mapping/parsing entry points.
- Standardize triggering-event context injection so event fields do not degrade to Object.

## Impact
- Impacted specs: `dsl-editor`
- Impacted code: `src/components/DslEditor.js`, `src/components/dsl_editor/ast-parse.js`, `src/dslValidation.js`, `src/components/AbilityEditor.js`, `src/components/ValueConfigEditor.js`
