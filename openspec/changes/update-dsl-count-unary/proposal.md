# Change: Update Count/Sum/Max/Min to Unary List Functions

## Why
The DSL aggregate functions currently support multiple signatures (multi-arg, property-based, and collection-based),
which creates ambiguity in the editor and makes type guidance unreliable. We want a single, predictable signature for
count/sum/max/min so the UI can guide users and validate types consistently.

## What Changes
- **BREAKING**: `count` remains unary only (`count(collection)` or `count("counterName")`).
- **BREAKING**: `sum`, `max`, `min` become unary only and accept a single numeric collection argument.
- **BREAKING**: remove legacy signatures (`sum(collection, property)`, `max(a,b,...)`, `min(collection, property)`).
- Update frontend filtering so these nodes only appear for numeric collections.

## Impact
- Affected specs: dsl-functions
- Affected code: Server/Domain/Abilities/DSL/Functions, Server/Services/DslMetadataService.cs,
  src/components/DslEditor.js, src/components/dsl_editor/options-provider.js
