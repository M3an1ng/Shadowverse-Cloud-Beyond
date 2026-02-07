## Context
Card pack definitions are now the source of truth, but the card editor still uses a free-form string input. We need a user-friendly way to reuse existing packs while safely adding new ones.

## Goals / Non-Goals
- Goals:
  - Autocomplete existing pack names/abbreviations/codes in the card editor.
  - Auto-create a new pack definition when a unique pack name is saved.
  - Keep pack input as a string field for UX continuity.
- Non-Goals:
  - Full card pack management UI (edit/delete).
  - Changing existing pack code semantics.

## Decisions
- Decision: Resolve pack input on save; if no existing pack matches by code/name/abbreviation, create a new pack definition.
- Decision: Use default region 0 for auto-registered packs and allocate the next available sequence starting at 2 (0 and 1 reserved).
- Decision: Autocomplete uses the existing card pack listing API with lightweight payload.
- Decision: Store card pack environments as a list and default auto-registered packs to `[Open]` with a blank abbreviation.

## Risks / Trade-offs
- Auto-registration may create unintended packs if users mistype. Mitigation: confirm on save or show an inline warning for new names.
- Sequence allocation must be concurrency-safe. Mitigation: enforce unique index on (Region, Sequence) and retry on conflict.

## Migration Plan
No data migration required; new packs are created on demand.

## Open Questions
- Should auto-registration be limited to admin/editor users only?
- Should the editor require explicit confirmation before creating a new pack?
