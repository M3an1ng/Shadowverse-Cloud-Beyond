## Context
The card editor currently fetches cards and abilities repeatedly across list view, ability list, and copy existing flow. The backend has GameDataProvider caching ability configs, but card/ability API endpoints still query the database directly and provide full JSON payloads when only counts are needed.

## Goals / Non-Goals
- Goals:
  - Phase A: reduce redundant client-side fetches via shared caches with deterministic invalidation.
  - Phase B: provide server-side revision signals and lightweight ability counts/summaries.
- Non-Goals:
  - Full offline support or long-lived persistent caching.
  - Changing gameplay runtime behavior.

## Decisions
- Decision: Adopt cache + revision (no realtime push) to balance performance and correctness.
- Decision: Phase A first, purely front-end caching with explicit invalidation on local mutations (no TTL).
- Decision: Phase B adds a server-side revision token to detect out-of-band changes and invalidate caches safely.
- Decision: Expose revision via response header `X-Data-Revision` for card/ability endpoints.
- Decision: Introduce a batch snapshot endpoint for ability counts to avoid loading full jsonConfig when only counts are needed.

## Risks / Trade-offs
- Stale data risk: mitigated by deterministic invalidation on local writes and revision tokens on server responses.
- Payload size vs complexity: summary endpoints reduce payload but add backend surface area.
- Revision changes are only observed on client requests; users see external changes on next request or manual refresh.

## Cache Strategy (Phase A)
- Cache scope: card editor session (in-memory only).
- Keys:
  - cards list
  - abilities-by-card
  - ability detail by id
- Invalidation:
  - On create/update/delete/copy ability: update affected caches or drop entries.
  - On card save/delete: invalidate cards list and relevant ability caches.
- No TTL expiry to avoid unexpected invalidation; manual refresh optional.

## Cache Strategy (Phase B)
- Add a revision token in GameDataProvider (e.g., Guid or long increment) that updates on RefreshCacheAsync.
- Expose revision via response header (X-Data-Revision).
- Client compares stored revision and clears caches on change.

## Migration Plan
- Phase A can ship independently with no API changes.
- Phase B adds backward-compatible endpoints/headers; client keeps fallback behavior when revision is missing.

## Open Questions
- None.
