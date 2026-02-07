# Change: Add preview description tooltips

## Why
Preview descriptions already highlight keyword/derived-card markers, but users cannot inspect what they mean. Clicking highlighted text should surface keyword definitions or derived-card details without leaving the preview.

## What Changes
- Render keyword/derived markers as interactive spans with metadata for lookup.
- Add a lightweight tooltip/overlay to show keyword definitions or derived-card details in battle preview and card editor preview.
- Add data sources for keyword descriptions and derived-card lookup (card definition cache + fallback behavior).
- Style interactive spans and the tooltip panel for clear affordance.

## Impact
- Affected UI: description parser, battle preview (ui.js), card editor preview (cardEditor.js), CSS.
- Affected data: keyword glossary data and card definition lookup cache.
- Potential API usage: `/api/localization/enums/*` and `/api/cards` for lookups.