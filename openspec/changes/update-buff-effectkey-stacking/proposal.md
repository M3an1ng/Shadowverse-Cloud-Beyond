# Change: update-buff-effectkey-stacking

## Why
现有 Buff 的 EffectKey 只在部分非叠加路径生成，且非叠加逻辑通过替换已有 Buff 实现，导致来源一致时的叠加/覆盖语义不一致。需要统一 EffectKey 的生成规则，并将叠加判定收敛到效果处理阶段，以避免重复 Buff 并保持 UI 叠加展示的一致性。

## What Changes
- 所有由能力效果创建的 Buff 都生成 EffectKey（sourceAbilityId:effectIndex），系统 Buff 不生成 key。
- Buff 相关效果参数统一提供 CanStack，用于在效果处理阶段决定是否允许相同 EffectKey 叠加。
- 当 CanStack 为 false 且目标已有相同 EffectKey 时，效果不生效，不替换已有 Buff。

## Impact
- 受影响的 specs: buff-stacking
- 受影响的代码: Server/Domain/Abilities/Effects/Handlers, Server/Domain/Abilities/Effects/Handlers/Parameters, Server/Domain/Buffs/BuffService.cs, Server/Domain/Buffs/BuffStackingRules.cs, Server/Domain/Events
