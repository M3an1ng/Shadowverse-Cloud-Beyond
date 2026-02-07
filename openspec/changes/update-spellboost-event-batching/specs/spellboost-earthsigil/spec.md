## ADDED Requirements
### Requirement: Spellboost Event Batching
系统 SHALL 在玩家施放法术后仅生成一次 SpellBoost 事件，并在事件目标中包含全部被增幅的卡牌。

#### Scenario: Spellboost batches targets
- **WHEN** 玩家施放法术且手牌中存在多张 Spellboost 卡牌
- **THEN** 只生成一个 SpellBoost 事件，且其目标列表包含所有被增幅卡牌