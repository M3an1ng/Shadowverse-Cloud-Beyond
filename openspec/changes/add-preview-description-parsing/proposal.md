# Change: Add preview description parsing

## Why
Card preview descriptions currently render as plain text, so keyword and derived-card references embedded with 【】/『』 are not visually distinguished. This makes previews harder to scan and blocks future enhancements like inline keyword/derived-card tooltips.

## What Changes
- Parse card description text in previews to detect keyword markers (【】) and derived-card markers (『』).
- Render marked segments with a dedicated highlight style (orange) matching the reference art direction.
- Add a small, reusable parsing utility so future expansions (tooltips, links) can reuse the token stream.

## Impact
- Affected UI: card editor preview and in-game battle card preview.
- Affected assets: add new CSS class for highlighted segments.
- No backend changes.