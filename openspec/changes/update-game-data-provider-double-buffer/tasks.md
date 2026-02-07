## 1. Implementation
- [x] 1.1 Introduce a CacheSnapshot container for card definitions, ability configs, buff definitions, and stats.
- [x] 1.2 Refactor GameDataProvider reads to reference the current snapshot instead of direct fields.
- [x] 1.3 Build cache snapshots in Load* methods without mutating live state.
- [x] 1.4 RefreshCacheAsync builds a new snapshot and atomically swaps it in.
- [x] 1.5 Update cache statistics derivation to use the active snapshot.
- [ ] 1.6 Validate refresh flow via diagnostics endpoint and manual refresh.
