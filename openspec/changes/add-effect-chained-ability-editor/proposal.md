# Change: Add chained ability editor in effect configuration

## Why
- The editor needs a dedicated UI for managing chained abilities attached to effects.
- New backend workflow requires chained abilities to be created with EffectCompletedEvent as the trigger and a stable order.

## What Changes
- Add a chained abilities editor entry point in the effect accordion header.
- Provide add/edit/delete operations for effect-level chained abilities.
- Create chained abilities with a locked trigger based on EffectCompletedEvent.
- Support drag-and-drop ordering and a visible sequence index.

## Impact
- Affected specs: ability-editor (new)
- Affected code: src/components/EffectConfigEditor.js, src/components/AbilityEditor.js, src/cardEditor.js, related styles
