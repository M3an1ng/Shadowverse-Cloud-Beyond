## Context

The game already has a play-mode pipeline for Base and Enhance through `PlayModeResolver`, `PlayCardRequestedEvent`, `PlayCardEvent`, and `PlayerState.ExecutePlayCard`. A partial Accelerate/Crystalize implementation exists: alternate play candidates can be selected from effective abilities and `PlayCardEvent.Commit` can transform the same `CardInstance` into a replacement definition before executing the play.

The missing architectural piece is transaction preflight. The action transaction service expands requests into `PlayCardEvent` roots before commit and analyzes primary abilities for target selection/cancellation. If alternate play transforms only during commit, target selection and pre-play validation can still inspect the original card definition instead of the replacement spell or amulet definition.

This design treats Accelerate and Crystalize as declaration abilities. They are effective abilities that decide whether the card may be played as another card definition; they are not normal triggered abilities that execute their own targets/effects. The referenced replacement card definition owns the actual play-time conditions, targets, and effects.

## Goals / Non-Goals

**Goals:**

- Preserve Shadowverse-style alternate play semantics for Accelerate and Crystalize.
- Keep Accelerate/Crystalize as abilities so they can be granted, removed, suppressed, and ordered with other effective abilities.
- Support multiple alternate play abilities on the same card.
- Select alternate play by highest affordable cost, with effective ability order as the tie-breaker.
- Use the referenced replacement spell/amulet definition for playability, target preflight, cancellation, commit, events, graveyard/field state, logs, and replay.
- Ensure cancellation before commit does not spend PP and does not permanently transform the card instance.
- Keep Enhance behavior unchanged.

**Non-Goals:**

- Do not make Accelerate/Crystalize declaration abilities run their own `Trigger`, `Targets`, or `Effects`.
- Do not introduce a separate runtime card instance for the replacement card.
- Do not implement an all-purpose card-copy or illusion system.
- Do not redesign the whole transaction service beyond the alternate-play preflight path needed here.
- Do not change deck construction or collection ownership rules for replacement definitions.

## Decisions

### Decision 1: Model alternate play as declaration metadata on abilities

Accelerate and Crystalize SHALL remain ability keywords, with an `AlternatePlay` metadata block that references `PlayAsCardDefinitionId`.

Rationale:

- The game already resolves effective abilities through ability manager state, so granted/removed/suppressed behavior falls out of the existing architecture.
- Multiple candidates can be ordered by the effective ability list.
- The replacement card can remain a normal card definition with normal spell/amulet ability configuration.

Alternatives considered:

- Put `AlternateCardDefinitionId` directly on `CardDefinition`: rejected because it would not support granted/removed alternate play abilities or multiple candidates.
- Encode replacement behavior as a normal effect: rejected because play-mode selection, target preflight, and UI playability need the replacement before effects execute.

### Decision 2: Resolver selects mode, ability, cost, and replacement definition as one unit

`PlayModeResolver` SHALL produce a single selected alternate candidate containing:

- `PlayMode.Accelerate` or `PlayMode.Crystalize`
- effective cost
- source ability runtime ID
- replacement card definition ID

Rationale:

- Mode and runtime ID must not be selected independently, or same-cost Accelerate/Crystalize candidates can mismatch.
- The request layer can reject client attempts to bypass priority by submitting a different ability runtime ID.

Alternatives considered:

- Group availability by mode first and choose mode later: rejected because it loses candidate ordering across different alternate play modes.

### Decision 3: Use a temporary replacement play view for preflight

Before commit, the transaction/playability path SHALL evaluate selected alternate play against a temporary replacement play view. The view exposes the same card instance identity but uses the replacement card definition and replacement ability configs for:

- field-space validation
- pre-play conditions
- primary play abilities
- target availability
- target selection and cancellation

Rationale:

- Cancellation must leave the real hand card unchanged.
- Target prompts must match the replacement spell/amulet.
- Effects and conditions authored on the replacement card must be reusable without copying them into the declaration ability.

Implementation approach:

- Keep `PlayCardEvent.AlternateCardDefinitionId` as the stable event payload.
- Add a small resolver/helper for alternate play context that can load the replacement definition and ability configs.
- For preflight, either create temporary runtime abilities bound to the same source `CardInstance`, or provide a transaction-scoped ability view that enumerates replacement play abilities without registering them globally.
- Do not call `CardInstance.Transform` during preflight.

Alternatives considered:

- Transform during request expansion and roll back on cancel: rejected because rollback would interact poorly with buffs, auras, ability registration, and user cancellation.
- Duplicate target/effect configuration onto the Accelerate/Crystalize ability: rejected because it makes database authoring error-prone and violates the replacement-card design.

### Decision 4: Permanently transform the same card instance on successful commit

On successful alternate play commit, the card instance SHALL transform to the replacement definition before normal play execution. After that point, field, graveyard, event snapshots, replay, and logs observe the replacement definition.

Rationale:

- This matches the requested distinction from Enhance.
- Existing transform code already unregisters old abilities, rebuilds new definition abilities, and invalidates derived state.
- Existing zone movement and spell/follower/amulet play logic can operate on the transformed type.

Alternatives considered:

- Store an `AlternateCardDefinitionId` overlay without transforming: rejected because it would make every downstream field/graveyard/replay/card mapping path branch on overlay state.

### Decision 5: Dedicated signal events are emitted after successful commit

Accelerate and Crystalize SHALL emit dedicated events after successful play state changes.

Rationale:

- Trigger listeners, logs, and replay can consume explicit event types.
- The events mirror `EnhanceEvent` but represent successful alternate play, not request selection.

### Decision 6: Editor treats declaration abilities as a special ability kind

The ability editor SHALL support configuring alternate play metadata on abilities with Accelerate/Crystalize keywords. It SHOULD avoid presenting declaration abilities as normal trigger/target/effect containers.

Rationale:

- Users need a clear place to set the replacement card definition.
- The actual target/effect authoring belongs to the replacement card definition.

## Risks / Trade-offs

- Preflight ability views may duplicate some ability creation logic → Mitigation: isolate the helper and keep it limited to play-time replacement abilities.
- Temporary abilities may accidentally be registered globally → Mitigation: tests must assert cancellation leaves original abilities/state unchanged.
- Existing playability code may still read the original `cardDefinition` in one branch → Mitigation: add tests for target availability and full-board Accelerate/Crystalize cases.
- Transform cleanup can remove buffs before play completes → Mitigation: this is expected once alternate play is confirmed; cancellation tests must ensure cleanup does not happen before commit.
- Editor users may configure a replacement definition of the wrong type → Mitigation: validate Accelerate references Spell and Crystalize references Amulet both in editor and server commit validation.

## Migration Plan

1. Keep existing Base and Enhance data unchanged.
2. Add alternate play metadata to Accelerate/Crystalize ability configs.
3. Add replacement Spell/Amulet card definitions as non-collectible or otherwise data-managed definitions according to current card data conventions.
4. Deploy backend resolver and transaction support before relying on frontend editor controls.
5. Rollback strategy: remove or disable Accelerate/Crystalize ability configs from card data; Base and Enhance remain unaffected.

## Open Questions

- Should replacement card definitions be required to be non-collectible, or is that a data policy outside this capability?
- Should `AvailabilityConditions` be supported immediately, or deferred until there is a concrete card needing conditional alternate play?
- Should logs display both original card name and replacement card name, or only the replacement card name after successful alternate play?
