# Change: Update Card Editor Data Cache

## Why
The card editor repeatedly queries CardDefinition and CardAbility data across multiple panels (list, abilities, copy existing). This causes avoidable load, slower UX, and noisy database traffic.

## What Changes
- Add a front-end shared cache in the card editor to reuse cards/abilities data and invalidate on local mutations.
- Add a backend revision token (cache version) so clients can detect data changes and invalidate caches on mismatch.
- Introduce a batch snapshot endpoint for ability counts to avoid full JSON loads when only counts are needed.

## Impact
- Affected specs: card-editor
- Affected code: src/cardEditor.js, Server/Controllers/AbilitiesController.cs, Server/Controllers/CardsController.cs (if present), Server/Services/GameDataProvider.cs
