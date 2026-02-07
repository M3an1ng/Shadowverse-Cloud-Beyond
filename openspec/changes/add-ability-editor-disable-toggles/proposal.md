# Change: Ability Editor Disable Toggles

## Why
Ability authors need a quick way to temporarily disable targets or effects without deleting their configuration.

## What Changes
- Add `IsEnabled` to `TargetConfig` and `EffectConfig` with a default of true.
- Skip disabled targets during target resolution and playability checks.
- Skip disabled effects when creating effect execution requests.
- Expose compact enable/disable toggles in target/effect headers in the ability editor.

## Impact
- Affected specs (new): `ability-editor-ux`
- Affected code:
  - `Server/Domain/Abilities/Targets/TargetConfig.cs`
  - `Server/Domain/Abilities/Effects/EffectConfig.cs`
  - `Server/Domain/Abilities/Targets/TargetSelectionService.cs`
  - `Server/Domain/Abilities/Ability.cs`
  - `src/components/TargetConfigEditor.js`
  - `src/components/EffectConfigEditor.js`
  - `src/components/AbilityEditor.js`
