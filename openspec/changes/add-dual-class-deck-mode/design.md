## Context

Current deck legality is mostly environment-centric and assumes single-class construction. The planned dual-class mode introduces new constraints (24/16 split, secondary class) that span persistence, validation, deck code, and match startup. The existing match pipeline also allows players to carry independent environment metadata without explicit start-time consistency checks. This design introduces a format model that separates card-pool scope from rule behavior while preserving backward compatibility for existing decks and clients.

## Goals / Non-Goals

**Goals:**
- Separate format dimensions into `environment` and `mode` across deck and match flows.
- Add `DualClass` mode with enforceable constraints: primary <= 24, secondary <= 16, total = 40.
- Keep legacy single-class behavior stable.
- Enforce same-format match startup (including AI fallback).
- Provide a migration path that does not break existing persisted decks and deck code imports.

**Non-Goals:**
- Introducing tournament/Bo3/ranked rule systems.
- Redesigning matchmaking queue strategy.
- Changing card content, card set definitions, or game balance values.

## Decisions

### Decision 1: Model format as `(environment, mode)` instead of a combined enum
- Choice: Keep `DeckEnvironment` for card-pool legality and add `DeckMode` for construction/match rules.
- Rationale: Prevents combinatorial enum growth and keeps rule ownership explicit.
- Alternative considered: Single combined enum (`OpenDualClass`, etc.). Rejected because every new mode/environment combination would require enum explosion and broad code rewrites.

### Decision 2: Persist `mode` and `secondaryClass` on deck entities
- Choice: Add `Mode` and nullable `SecondaryClass` to deck persistence and API DTOs.
- Rationale: Deck legality and import/export must be deterministic without inferring mode from cards each time.
- Alternative considered: Infer dual-class from card list. Rejected due to ambiguity and unstable behavior on edits.

### Decision 3: Centralize legality in rule-set-driven deck validation
- Choice: Extend `DeckValidationService` to resolve constraints from `(environment, mode)` and apply mode-specific class quota checks.
- Rationale: Single source of truth for server-side legality across deck CRUD, import, and match preflight.
- Alternative considered: Split checks across controller/service call sites. Rejected due to duplication and drift risk.

### Decision 4: Neutral policy for dual-class mode
- Choice: Neutral cards count toward total 40 but do not consume primary/secondary class quotas.
- Rationale: Keeps dual-class quotas focused on class distribution and maintains predictable deck construction UX.
- Alternative considered: Count neutral toward one/both class quotas. Rejected due to unclear user expectations and harder UX messaging.

### Decision 5: Match start must enforce format consistency
- Choice: Validate host/opponent (or host/AI) environment+mode equality before game initialization.
- Rationale: Avoids mixed-rule matches and analytics contamination.
- Alternative considered: Allow mixed formats and resolve per-player at runtime. Rejected as rules become non-comparable and brittle.

### Decision 6: Versioned deck code compatibility
- Choice: Extend deck code with a new version carrying mode/secondary-class metadata; keep legacy decode defaults.
- Rationale: Enables new functionality without breaking existing saved/shared codes.
- Alternative considered: Break and replace old code format. Rejected due to migration and UX cost.

## Risks / Trade-offs

- [Risk] Legacy clients may omit `mode`/`secondaryClass` and trigger validation errors in dual-class flows.
  - Mitigation: Keep defaults for standard mode and return explicit validation errors only when dual-class is selected.

- [Risk] Migration backfill may mis-handle existing null/unknown deck records.
  - Mitigation: Add additive columns with safe defaults (`Mode=Standard`, `SecondaryClass=NULL`) and verify read paths.

- [Risk] Match start strictness can block previously permissive edge cases.
  - Mitigation: Emit explicit mismatch error messages and keep host/client precheck hints in deck selection UI.

- [Trade-off] Added schema and DTO fields increase short-term code surface.
  - Benefit: Long-term extensibility for additional modes without refactoring environment semantics.

## Migration Plan

1. Add enum and data model fields:
   - Add `DeckMode` enum.
   - Add `Mode` + `SecondaryClass` to `UserDeck`.
   - Add corresponding fields to deck and match DTOs.

2. Add database migration:
   - Add `UserDecks.Mode` (non-null default `Standard`).
   - Add `UserDecks.SecondaryClass` (nullable).

3. Update validation and services:
   - Extend `DeckValidationService` signature and call sites.
   - Implement mode-aware quota checks and class legality checks.
   - Apply match format consistency checks for PvP and AI.

4. Update deck code compatibility:
   - Introduce versioned encode/decode branch.
   - Map legacy decode to standard defaults.

5. Update frontend contract:
   - Add mode/secondary class fields in deck builder requests.
   - Add dual-class quota display and guardrails.

6. Verification and rollout:
   - Add/adjust tests for validation, import/export, and match mismatch behavior.
   - Deploy migration and server first, then UI changes.

### Rollback Strategy
- If production issues appear, rollback to previous application build and keep additive DB columns unused.
- Because migration is additive, rollback does not require destructive schema operations.

## Open Questions

- Should dual-class require an exact 24+16 split or allow smaller class counts as long as total deck size is valid? (current design enforces upper bounds + existing total-size rules)
- Should mode/environment be included in winrate aggregate primary keys immediately, or follow in a separate analytics change?
- Should AI deck selection API explicitly carry mode/environment fields, or always derive from selected deck records?
