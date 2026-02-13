# Change: Update ability trigger ordering by zone position

## Why
Current trigger resolution does not enforce a strict in-zone order by battlefield position (entry/placement order). In same-zone scenarios, abilities can resolve in registration-dependent order, which causes nondeterministic behavior and test instability.

## What Changes
- Introduce a unified trigger ordering policy used by ability trigger collection for `Before`/`On`/`After` timings.
- Define a deterministic ordering key that preserves existing special priorities (attacker/defender/source), then enforces in-zone ordering by `ZoneIndex`.
- Add stable tie-breakers (`ZoneEnterSequence`, in-card ability order, runtime registration order) for fully deterministic outcomes.
- Normalize zone state tracking so zone position and zone-entry sequence remain consistent when cards enter/move/leave zones.
- Add targeted tests and diagnostics for same-zone ordering and cross-zone ordering compatibility.

## Impact
- Affected specs: `ability-trigger-ordering`
- Affected code:
  - `Server/Domain/Abilities/AbilityManager.cs`
  - `Server/Domain/Constants/GameConstants.cs`
  - `Server/Domain/Entities/CardInstance.cs`
  - `Server/Domain/Entities/PlayerState.cs`
  - `Server/Domain/Factories/CardFactory.cs`
  - `Server/Services/GameActionTransactionService.cs`
  - `Server.Tests/*` (trigger ordering and integration coverage)
