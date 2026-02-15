## 1. Persistence Mode
- [x] 1.1 Add replay persistence mode option (default raw-only).
- [x] 1.2 Update transaction recording path to stop persisting `ClientDtos` in raw-only mode.
- [x] 1.3 Ensure journal writes remain best-effort and do not affect match commit behavior.

## 2. Replay Read Path
- [x] 2.1 Add raw-event projection path for `CatchUp` when `ClientDtos` are empty.
- [x] 2.2 Add raw-event projection path for `Seek` after snapshot restore.
- [x] 2.3 Keep legacy compatibility for rows that still include `ClientDtos`.

## 3. Correctness Guards
- [x] 3.1 Enforce replay reconstruction as apply-only (no `Commit()` re-execution).
- [x] 3.2 Add duplicate-apply guard using sequence/event identity.

## 4. Validation
- [x] 4.1 Add replay parity tests: legacy DTO path vs raw projection path outputs.
- [x] 4.2 Add integration test: snapshot + raw range replay reaches expected state/event stream.
- [ ] 4.3 Validate storage reduction on representative matches.
