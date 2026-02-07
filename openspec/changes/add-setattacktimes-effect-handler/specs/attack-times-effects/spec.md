## ADDED Requirements
### Requirement: SetAttackTimes Effect Processing
系统 SHALL 支持 SetAttackTimes 效果，用于设置目标随从每回合可攻击次数上限，并持久化到卡牌实例状态。

#### Scenario: Apply max attacks per turn
- **WHEN** 一个能力对随从目标触发 SetAttackTimes（MaxAttacksPerTurn = 2）
- **THEN** 目标随从的最大攻击次数应更新为 2，并在后续回合攻击检查中生效
