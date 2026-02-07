## Context
- Card definitions currently do not persist art assets.
- Static files are served from the workspace root, so assets under /assets are accessible by URL.

## Goals / Non-Goals
- Goals: allow optional preview/detail images per card; support separate uploads; use artPreview for in-editor preview; open artDetail from deck builder.
- Non-Goals: image editing, version history, CDN integration, or mandatory art requirements.

## Decisions
- Decision: Store artPreview/artDetail as URL paths in CardDefinition.
  - Reason: minimal changes, works with existing static file hosting.
- Decision: Save files under assets/cards/{cardId}/.
  - Reason: predictable structure and easy cleanup/override.

## Risks / Trade-offs
- Unbounded storage growth if old files are not cleaned up; mitigate by overwriting and deleting previous files when paths are replaced.
- Large images could impact load time; mitigate by only loading detail art on explicit user action.

## Migration Plan
- Add nullable columns to CardDefinitions.
- Deploy and migrate DB; no data backfill required.

## Open Questions
- None.
