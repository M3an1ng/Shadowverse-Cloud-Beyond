# Change: Update Card Detail View

## Why
The card-detail mockup is still a standalone route with static placeholders, while multiple screens open an image-only detail modal. We need a single, data-bound detail experience that can be opened from deck builder and preview surfaces without routing.

## What Changes
- Replace the standalone card-detail-mockup route with a reusable card detail modal based on the mockup layout.
- Bind card detail fields to real card data (name, class, type, cost, attack, defense, description, preview art).
- Rename the evolve button to an inspect action that opens the detail art viewer.
- Update deck builder and preview entry points to open the new detail modal instead of the legacy image-only modal.

## Impact
- Affected specs: card-detail-view
- Affected code: src/components/CardDetailMockup.js, src/styles/card-detail-mockup.css, src/deckBuilder.js, src/cardEditor.js, src/previewDescriptionTooltip.js, src/ui.js, src/router.js, index.html
