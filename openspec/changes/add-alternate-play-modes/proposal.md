## Why

The card engine needs first-class support for Shadowverse-style Accelerate and Crystalize so high-cost cards can be played as configured replacement spell or amulet cards when their base cost is unaffordable. The current partial implementation selects alternate play modes and transforms on commit, but it does not yet cover preflight target resolution, playability checks, editor configuration, or replay/log behavior as a complete contract.

## What Changes

- Add alternate play declaration support for abilities carrying `Accelerate` or `Crystalize` keywords.
- Allow multiple Accelerate/Crystalize abilities on the same card and select the highest affordable cost; for tied costs, select the earlier effective ability.
- Require alternate play abilities to reference an existing replacement card definition.
- Treat Accelerate as playing the same card instance as the referenced spell definition.
- Treat Crystalize as playing the same card instance as the referenced amulet definition.
- Ensure field, graveyard, event history, replay, and logs observe the replacement definition after successful alternate play.
- Ensure target selection, pre-play validation, field-space checks, and commit behavior use the replacement card definition when alternate play is selected.
- Emit dedicated Accelerate and Crystalize events after successful alternate play for triggers, logs, and replay.
- Expose editor support for configuring replacement card definitions while making it clear that normal trigger/target/effect configuration belongs on the replacement card, not on the declaration ability.
- No breaking change to existing Base or Enhance play mode semantics.

## Capabilities

### New Capabilities

- `alternate-play-modes`: Defines Accelerate and Crystalize alternate play behavior, selection priority, validation, replacement card transformation, event emission, playability, and editor-facing configuration rules.

### Modified Capabilities

- None.

## Impact

- Domain ability configuration:
  - `AbilityConfig`
  - `IAbility`
  - runtime ability creation and effective ability filtering
- Play mode resolution and request validation:
  - `PlayModeResolver`
  - `PlayCardRequestedEvent`
  - `PlayCardEvent`
  - `PlayerState.PreparePlayCard`
  - `PlayerState.ExecutePlayCard`
- Transaction preflight and target resolution:
  - `GameActionTransactionService`
  - `TargetSelectionService`
  - card playability checks in `GameService`
- Events, replay, and logs:
  - `EventType`
  - `AccelerateEvent`
  - `CrystalizeEvent`
  - event DTO/log conversion paths
- Frontend and editor:
  - card playability UI
  - hand cost display
  - field-space client guard
  - ability editor controls for alternate play metadata
- Tests:
  - resolver priority tests
  - request validation tests
  - commit transformation tests
  - target preflight/cancel tests
  - Crystalize field-space tests
  - replay/log/event tests
