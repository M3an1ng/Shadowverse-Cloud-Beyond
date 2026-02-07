# Change: Preserve DSL parentheses as encapsulation

## Why
The DSL editor currently loses explicit parentheses during parse-to-UI mapping, so saved expressions cannot restore encapsulated sub-expressions. Users want to preserve the brackets they explicitly saved and restore those encapsulations on load.

## What Changes
- Add an explicit Parenthesized/Grouped expression node to the DSL AST to preserve parentheses in the parse API.
- Map Parenthesized nodes to encapsulated UI nodes when rebuilding the editor AST.
- Ensure evaluation/compiler paths unwrap Parenthesized nodes without changing semantics.

## Impact
- Specs: dsl-editor
- Code: Server/Domain/Abilities/DSL/AST, Server/Domain/Abilities/DSL/Parser, Server/Controllers/DslController.cs, src/components/dsl_editor/ast-mapping.js
