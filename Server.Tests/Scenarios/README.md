# Card Scenario Tests

## Purpose
Provide a data-driven scenario harness to validate card behavior using the real game-flow pipeline.

## Adding Scenarios
- Add a new `ScenarioCase` to the appropriate `<Craft>Scenarios.cs` file and include it in `Cases`.
- Configure `Setup`, `Actions`, `Choices`, and `Expectations` so each scenario is self-contained.
- Keep `FocusCardId` aligned with `data/card_data_*.json` and register any required supporting cards in `Setup.Cards`.
- Use the choice key rules below when a scenario needs deterministic player selections.
- Run `dotnet test Server.Tests/ShadowverseBeyond.Server.Tests.csproj -c Release --filter FullyQualifiedName~CardScenarioTests`.

## Action Lifecycle Mapping

### PlayCard
1. `PlayCardRequestedEvent` validates PP and play mode and emits `PlayCardEvent`.
2. `PlayCardEvent.Commit` pays cost, increments play count, and emits `SummonEvent` (followers/amulets) or moves spells to graveyard.
3. Ability triggers are resolved through `GameActionTransactionService` (Before/On/After), effects are executed, and resulting events are committed recursively.

### Evolve
1. `EvolveRequestedEvent` validates EP and evolve rights, then emits `EvolveEvent`.
2. `EvolveEvent.Commit` applies evolution state changes and emits stat change events.
3. Abilities listening to `EventType.Evolve` trigger on the committed `EvolveEvent`.

### SuperEvolve
1. `SuperEvolveRequestedEvent` validates SEP and rights, then emits `EvolveEvent` and `SuperEvolveEvent`.
2. `SuperEvolveEvent.Commit` applies super evolution state changes and emits stat change events.
3. Abilities listening to `EventType.SuperEvolve` trigger on the committed `SuperEvolveEvent`.

### Act
1. `ActRequestedEvent` validates card ownership/zone and emits `ActInvokedEvent`.
2. Abilities listening to `EventType.Act` trigger on the committed `ActInvokedEvent`.

### Choose / Target Selection
`IPendingActionService` resolves player choice requests deterministically during tests. Scenarios can provide explicit choice mappings; otherwise the default strategy selects the first valid targets.

#### Choice keys
- Target arrays with a single entry are stored under the key `default`.
- For `Choose` effects, the target key is `choose_{effectIndex}` (0-based index within the effect list).
- Choice lookup order: `{AbilityName}.{TargetKey}` → `{CardKey}.{TargetKey}` → `{TargetKey}`.

#### Choice values
- `CardKeys`: list of scenario card keys to pick (mapped to instance ids).
- `Indices`: list of indices into `SelectableTargetIds` (0-based).
