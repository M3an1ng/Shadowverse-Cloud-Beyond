## 1. Implementation (Phase A: Frontend Cache)
- [x] 1.1 Audit all card editor data fetches and map required fields for each panel.
- [x] 1.2 Introduce shared in-memory caches for cards, abilities-by-card, and ability details in CardEditor.
- [x] 1.3 Ensure all local mutations (create/update/delete/copy) update or invalidate the caches deterministically.
- [x] 1.4 Add a manual refresh path (optional) to force re-fetch from server.

## 2. Implementation (Phase B: Backend Cache + Revision)
- [x] 2.1 Define a cache revision token from GameDataProvider (e.g., Guid or increment) updated on RefreshCacheAsync.
- [x] 2.2 Expose revision on card/ability endpoints (response header: X-Data-Revision).
- [x] 2.3 Add a batch snapshot endpoint for ability counts to avoid full JSON for list views.
- [x] 2.4 Update frontend to invalidate caches when revision changes.

## 3. QA
- [ ] 3.1 Manual: open card editor, switch cards/tabs repeatedly; verify no redundant network calls.
- [ ] 3.2 Manual: create/update/delete ability; verify list and copy modal reflect changes without stale data.
- [ ] 3.3 Manual: refresh revision (simulate server update); verify client cache invalidates and reloads.
