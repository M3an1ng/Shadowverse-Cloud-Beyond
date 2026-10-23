## 1. Data Model and Registry

- [x] 1.1 Add persistent `TribeDefinition` storage model, EF mapping, and migration for canonical name, aliases, behavior, reserved flag, and enabled flag.
- [x] 1.2 Extend game data loading to read tribe definitions and expose a refreshable tribe registry alongside existing card data caches.
- [x] 1.3 Add bootstrap/backfill logic that registers existing card-definition tribes and seeds the required special tribe definitions.

## 2. Write-Path Normalization

- [x] 2.1 Normalize and auto-register tribe values in card create/update flows before persistence.
- [x] 2.2 Normalize and auto-register tribe values during seed import and other offline data-loading paths.
- [x] 2.3 Update AddTribe and related runtime tribe-granting entry points to normalize tribe values and reject invalid reserved-name usage.

## 3. Runtime Tribe Semantics

- [x] 3.1 Implement `ITribeRegistry` and `ITribeSemantics` services covering canonicalization, alias resolution, expansion, membership checks, and overlap checks.
- [x] 3.2 Update `CardInstance` tribe merge/add/remove behavior to use canonical tribe values and semantic deduplication.
- [x] 3.3 Ensure special match-all tribe values remain stored canonically but expand only during semantic evaluation.

## 4. DSL Compatibility

- [x] 4.1 Replace direct tribe collection equality handling in `ExpressionTreeBuilder` with tribe-semantic membership evaluation while preserving existing DSL syntax.
- [x] 4.2 Update `ContainsFunction` so tribe collections use the same semantic comparison path as equality.
- [x] 4.3 Add DSL-focused tests covering canonical tribe matching, alias matching, and special match-all tribe behavior.

## 5. Leave-Field History Selection

- [ ] 5.1 Add a leave-field history tribe candidate builder that reads name and tribe data from event history snapshots rather than graveyard or destroy-only summaries.
- [ ] 5.2 Implement candidate expansion for multi-tribe cards and special match-all tribe cards.
- [ ] 5.3 Implement tribe-distinct selection rules that consume the selected card and all of its effective tribes after a choice is made.
- [ ] 5.4 Wire the new history tribe candidate flow into the Pocket Galaxy implementation path.

## 6. Validation and Coverage

- [ ] 6.1 Add integration tests for card save/import/add-tribe normalization and auto-registration behavior.
- [ ] 6.2 Add integration tests for leave-field history selection, including non-destroy leave events, multi-tribe cards, and special tribe expansion.
- [ ] 6.3 Add focused regression tests proving a selected multi-tribe historical card cannot be chosen again through another expanded tribe candidate.
- [x] 6.4 Run `openspec validate update-tribe-semantics-and-pocket-galaxy --strict`.
