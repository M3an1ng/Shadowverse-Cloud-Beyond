# Change: Spellboost 事件批量化

## Why
- 当前 Spellboost 在玩家施放法术后会对每张目标卡牌生成单独的 SpellBoostEvent，事件数量随手牌规模线性增长，导致事件处理与日志噪声偏高。
- 批量事件更贴近“一个法术触发一次 Spellboost”的语义，并便于能力触发与回放统一处理。

## What Changes
- SpellBoostEvent 改为一次性携带所有被增幅的卡牌作为目标。
- Spellboost 事件只在一次施放法术时生成一次；不再为每张目标卡牌单独生成。

## Impact
- Affected specs: spellboost-earthsigil
- Affected code: Server/Domain/Entities/PlayerState.cs, Server/Domain/Events/SpellBoostEvent.cs