# Change: AuraConfig Produces Buff Instances

## Why
目前持久性能力需要通过 AddAuraEffectParams 显式配置，语义上仍与一次性 Effect 混在一起，不利于将“持久光环”作为稳定的 Buff 生命周期管理。

## What Changes
- Introduce AuraConfig as a first-class ability-level configuration that produces Buff instances via AuraManager.
- Keep Ability-level Targets/Conditions/Properties as defaults, with AuraConfig optional overrides.
- AuraConfig defines a direct Buff template (no Effect/EffectConfig in the aura path).
- Use Conditions as the overall on/off gate for the aura (refresh proceeds only when conditions pass).

## Impact
- Specs: persistent-auras
- Code: AbilityConfig, DSL/JSON loaders, AuraManager mapping, Ability execution pipeline, tests
