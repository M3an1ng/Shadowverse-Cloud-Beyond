# Change: Unify Aura Execution With Ability Pipeline

## Why
当前 Aura 通过独立的 RefreshAuras 流程更新 Buff，与 Ability 的统一触发/解析/执行链路分离，导致语义不一致、生命周期割裂，且能力影响能力的规则难以统一。

## What Changes
- Introduce a unified execution path so aura refresh is triggered via AbilityManager’s pipeline.
- Reuse Ability’s trigger/condition/target resolution, while branching execution into Aura/Buff refresh instead of Effect creation.
- Narrow AuraManager to buff-only lifecycle management (apply/remove/diff), removing target/condition/trigger logic.

## Impact
- Specs: persistent-auras
- Code: AbilityManager, Ability processing pipeline, AuraManager/Aura executor, trigger/target utilities, tests
