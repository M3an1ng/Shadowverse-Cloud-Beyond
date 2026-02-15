# Change: Persist replay as raw-event facts only (stop DTO persistence)

## Why
- Replay storage is dominated by serialized client DTO payloads, causing rapid SQLite growth and higher write pressure.
- The system already records `RawEventJson` as the canonical fact model for replay/event history.
- We need to reduce storage risk first without changing real-time gameplay broadcasting behavior.

## What Changes
- Keep writing full committed event facts to journal via `RawEventJson` for all persisted replay events.
- Stop persisting `ClientDtos` by default (raw-only persistence mode).
- Keep real-time DTO generation/broadcast unchanged during live matches.
- Update replay read path (`CatchUp`/`Seek`) to work when `ClientDtos` are empty by projecting from raw events.
- Keep compatibility for legacy records that still contain `ClientDtos`.

## Impact
- Affected capabilities: replay journal persistence, replay catch-up/seek pipeline, raw-event projection.
- Affected code: transaction artifact building, journal write model, replay hub/controller read flow.
- Risk: projection parity regressions if raw->client mapping is incomplete; must be covered by replay parity tests.
