## 1. Implementation
- [ ] 1.1 Add CardDefinition Code/CardVersion fields, indexes, and migration/backfill for 8-digit Code.
- [ ] 1.2 Update DTOs/mappings and API validation; reject edits to non-latest versions.
- [ ] 1.3 Implement server-side Code generation and new-version creation endpoint with uniqueness safeguards.
- [ ] 1.4 Change ability/art save flow to persist only on card save; ensure version-specific assets and abilities.
- [ ] 1.5 Update deck storage to use Code; resolve to latest version at play time.
- [ ] 1.6 Add deck code v2 using Code; keep legacy decode compatibility.
- [ ] 1.7 Add match/replay snapshot storage and replay resolution from snapshots; archive or block deletion of referenced versions.
- [ ] 1.8 Update UI for version switching and save-as-version behavior; deck builder uses Code.
- [ ] 1.9 Add tests for code generation, deck code compatibility, and replay snapshot stability.
