## Why

Current deck rules conflate card-pool legality and gameplay/deck-construction rules under a single "environment" concept, which makes rule evolution fragile and creates format explosion risk (`Open_DualClass_Bo3`, etc.). We need a stable two-dimensional model: `Environment` controls legal card pool, `Mode` controls construction/match rules. This change also introduces a concrete new mode (dual-class) required by product design: primary class up to 24 cards + secondary class up to 16 cards.

## What Changes

- Introduce explicit `DeckMode` (independent from `DeckEnvironment`) in deck domain model, DTOs, and match deck selection payloads.
- Define a rule resolution pipeline that validates decks by `(environment, mode)` instead of environment-only/hard-coded checks.
- Add new `DualClass` mode with strict composition limits:
  - one primary class and one secondary class,
  - primary-class cards up to 24,
  - secondary-class cards up to 16,
  - total deck size remains 40.
- Establish neutral-card policy for dual-class validation and expose it consistently across backend/frontend contracts.
- Extend deck validation error model to return explicit, user-facing failure reasons for mode/class quota violations.
- Enforce match start consistency: both players must use the same `environment` and `mode` (including AI fallback path).
- Add backward-compatible deck code/import behavior:
  - legacy deck codes continue to decode as standard/open defaults,
  - new code version can carry mode + secondary class context.
- Update deck-builder contracts for mode selection, secondary class selection, and real-time quota indicators.
- Non-goals in this change:
  - no Bo3/tournament flow,
  - no ranked season matchmaking redesign,
  - no card balance/content changes.

## Capabilities

### New Capabilities

- `deck-format-dimensions`: Support independent `environment` and `mode` dimensions across deck storage, validation, and match selection.
- `dual-class-construction-rules`: Enforce dual-class deck construction with primary/secondary class assignment and 24/16 quota limits under 40-card total.
- `match-format-consistency`: Require both sides to share the same environment and mode before match start, including AI deck selection path.
- `deck-code-mode-compatibility`: Support mode-aware deck code import/export while preserving backward compatibility for legacy codes.

### Modified Capabilities

- None. (No baseline specs currently exist under `openspec/specs/`; this proposal introduces new baseline capabilities.)

## Impact

- Affected code:
  - `Server/Dtos/Enums/GameEnum.cs` (add `DeckMode` and related enum handling)
  - `Server/Domain/Entities/UserDeck.cs`, `Server/Domain/Entities/MatchDeckSelection.cs`, `Server/Domain/Entities/PlayerState.cs` (mode/secondary class persistence and runtime projection)
  - `Server/Dtos/DeckDtos.cs` and match request DTOs (public API contract updates)
  - `Server/Services/DeckValidationService.cs` (mode-aware quota and class legality validation)
  - `Server/Services/DeckCodeService.cs` (versioned encode/decode compatibility path)
  - `Server/Services/GameService.cs`, `Server/Services/MatchService.cs`, `Server/Controllers/MatchController.cs` (format consistency enforcement and selection resolution)
  - `src/deckBuilder.js` (mode selector, secondary class selector, and quota UI)
- Affected APIs:
  - Deck create/update/import payloads and responses add `mode` and optional `secondaryClass`.
  - Match deck selection payload includes `mode` and optional `secondaryClass` context.
  - Error responses include mode/quota-specific validation failures.
- Data/storage:
  - `UserDecks` schema requires additive fields for `Mode` and `SecondaryClass` (nullable for legacy decks).
  - Existing deck records need default backfill to standard/open semantics.
- Compatibility/risk:
  - Existing clients that ignore new fields continue to work in standard mode.
  - Match start validation becomes stricter for mixed format selections.
- Testing:
  - Add validation tests for 24/16 quotas, total size, neutral policy, and off-class rejection.
  - Add integration tests for deck CRUD/import under `(environment, mode)` combinations.
  - Add match flow tests for environment/mode mismatch rejection and AI selection consistency.
