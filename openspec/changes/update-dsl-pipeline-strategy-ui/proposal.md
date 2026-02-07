# Change: Update DSL Pipeline Strategy UI

## Why
Designers cannot preserve or configure orderBy direction and take strategies in the visual pipeline editor. The current UI drops these semantics, forcing manual DSL edits.

## What Changes
- Add UI controls to select orderBy direction (asc/desc).
- Add UI controls to select take strategy (first/last/random/all) and numeric count input.
- Simplify take count editing to a numeric input without self-context DSL nodes.
- Preserve pipeline strategy choices when parsing and rendering existing expressions.

## Impact
- Affected specs: dsl-editor
- Affected code: Server/Controllers/DslController.cs, Server/Dtos/Dsl/DslParseDtos.cs, src/components/dsl_editor/*
