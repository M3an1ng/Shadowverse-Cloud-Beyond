# Change: Separate Card Sets from Purchasable Card Packs

## Why
Card set ownership (deck legality and environment) is currently mixed with purchasable card pack metadata, which causes ambiguous behavior in the editor and in environment checks. We need a clear separation: card sets define legality and environments, while card packs are purely purchasable products.

## What Changes
- Introduce CardSetDefinition as the canonical source for card set identity and environments.
- Link CardDefinition to CardSetDefinition and resolve card_set input by matching the set name; if auto-registering, use the input as the set name and leave abbreviation blank.
- Update card set input in the editor to match by set name only and avoid showing abbreviation/code fields.
- Make CardPackDefinition reference exactly one CardSetDefinition and remove environment responsibilities from card packs.
- Stop auto-creating CardPackDefinition on card save; only CardSetDefinition is auto-registered.

## Impact
- Affected specs: card-set-catalog, card-pack-catalog.
- Affected areas: card editor input resolution, deck legality checks, EF Core schema/migrations, and economy pack lookup.
- Related active changes: add-cardpack-editor-autocomplete, add-cardpack-code-and-environment (requires alignment when implementing).
