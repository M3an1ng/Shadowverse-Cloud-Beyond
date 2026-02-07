## ADDED Requirements
### Requirement: SpellBoost Effect Processing
系统 SHALL 提供 SpellBoost 效果，使用效果目标列表生成一次 SpellBoostEvent 以触发魔力增幅。

#### Scenario: SpellBoost effect emits a grouped event
- **WHEN** SpellBoost 效果执行并选择了卡牌目标
- **THEN** 系统生成一次 SpellBoostEvent，其目标包含所选卡牌
