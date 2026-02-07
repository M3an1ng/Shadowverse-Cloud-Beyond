## Context
- Preview descriptions already parse and highlight keyword/derived-card markers.
- Bracket text is localized (e.g., Chinese), so token lookup needs a mapping to enum keys.

## Goals / Non-Goals
- Goals:
  - Click keyword/derived references to show a definition panel.
  - Support both battle preview and card editor preview.
  - Keep parsing reusable for future expansions.
- Non-Goals:
  - Full card detail view or editing workflows.
  - Overhauling the localization system.

## Decisions
- Add data attributes to keyword/derived spans (type + raw text) so UI can handle clicks via event delegation.
- Maintain a keyword glossary map keyed by enum name, and resolve bracket text using localization maps from `/api/localization/enums/CardKeyword` and `/api/localization/enums/AbilityKeyword`.
- Resolve derived-card references via a cached card definition lookup loaded on demand from `/api/cards`.

## Risks / Trade-offs
- Loading all card definitions on demand could be heavy as the dataset grows; cache results and lazy-load on first use.
- Bracket text may not match any known keyword; provide a user-facing fallback message.

## Migration Plan
- No migration; additive UI enhancement.

## Open Questions
- Confirm whether tooltip should be click-to-toggle only or also support hover.
- Confirm desired tooltip layout (include art thumbnails for derived cards?).