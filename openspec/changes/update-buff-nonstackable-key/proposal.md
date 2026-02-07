# Change: update-buff-nonstackable-key

## Why
当前 isNonStackable 仅按 SourceAbilityId 判定替换，会导致同一能力下不同 Effect 产生的 Buff 互相覆盖，破坏设计预期（例如关键词 Buff 被 Ability Buff 覆盖）。需要引入 Effect 级别的区分，并将叠加替换规则收敛为 “按 key 替换”。

## What Changes
- 为 Effect 增加稳定的 EffectIndex，并在创建时写入。
- Buff 增加 NonStackableKey，用于非叠加替换的判定（由效果配置决定是否写入）。
- isNonStackable 仅保留在效果参数中，用于决定是否写入 NonStackableKey；Buff 实体不再包含 IsNonStackable。
- BuffStackingEngine 按 NonStackableKey 执行替换；未设置 key 的 Buff 默认可叠加。

## Impact
- 受影响的代码: Server/Domain/GameFlow/EffectQueue/Effect.cs, Server/Domain/Abilities/Ability.cs, Server/Domain/Buffs/Buff.cs, Server/Domain/Buffs/BuffService.cs, Server/Domain/Buffs/BuffStackingRules.cs
- 受影响的行为: 非叠加替换规则由 “同能力” 改为 “同能力 + 同效果（key）”。
