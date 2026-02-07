# Change: Card pack editor autocomplete and auto-registration

## Why
Card pack entry in the card editor is currently a free-form string, which leads to typos and inconsistent pack naming. We need inline suggestions and a safe path to register new packs.

## What Changes
- Add autocomplete suggestions for existing card packs while typing in the card editor.
- Auto-register a new card pack definition when a user saves a card with a brand-new pack name.
- Reserve sequence 0 and 1 in the default region; auto-registration starts from sequence 2.
- Store card pack environments as a list and default auto-registered packs to Open with a blank abbreviation.

## Impact
- Affected specs: card-pack-catalog
- Affected code: src/cardEditor.js, backend pack resolution/creation, card pack definition APIs
