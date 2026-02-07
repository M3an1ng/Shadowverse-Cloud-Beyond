## ADDED Requirements
### Requirement: Decision Effects (Random/Choose)
系统 SHALL 支持 Random 与 Choose 两类决策型效果，用于从多个分支中生成结果并仅执行被选中的链式能力。

#### Scenario: Random selects multiple branches by weight
- **WHEN** 一个能力配置 RandomEffect，包含多条分支并设置权重与选择数量
- **THEN** 系统应按权重随机选择指定数量的分支，并仅触发被选中的链式能力

#### Scenario: Choose selects multiple branches by player choice
- **WHEN** 一个能力配置 ChooseEffect，允许玩家选择多个分支
- **THEN** 系统应提示玩家选择，并仅触发被选中的链式能力

### Requirement: Decision Output Exposure
系统 SHALL 将 Random/Choose 的决策结果输出到 EffectCompletedEvent 的 Outputs 字段，供后续能力判定使用。

#### Scenario: Decision outputs available to chained abilities
- **WHEN** Random/Choose 完成并生成选项结果
- **THEN** EffectCompletedEvent.Outputs 应包含被选中的选项索引/ID与选择数量
