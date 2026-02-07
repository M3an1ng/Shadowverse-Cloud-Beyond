## MODIFIED Requirements
### Requirement: Decision Effects (Random/Choose)
系统 SHALL 支持 Random 与 Choose 两类决策型效果，并使用 AbilityConfigs 参数提供的分支能力列表生成结果；系统 MUST NOT 从 chainedAbilities 推断分支。

#### Scenario: Random selects multiple branches by weight
- **WHEN** 一个能力配置 RandomEffect，包含 AbilityConfigs 分支列表并设置权重与选择数量
- **THEN** 系统应按权重随机选择指定数量的分支，并仅触发被选中的分支能力

#### Scenario: Choose selects multiple branches by player choice
- **WHEN** 一个能力配置 ChooseEffect，包含 AbilityConfigs 分支列表并允许玩家选择多个分支
- **THEN** 系统应提示玩家选择，并仅触发被选中的分支能力

#### Scenario: Missing branch list is invalid
- **WHEN** Random/Choose 未提供 AbilityConfigs 或列表为空
- **THEN** 系统 MUST 视为配置无效并拒绝执行该效果

## ADDED Requirements
### Requirement: Decision Branch Ordering
系统 SHALL 先执行 Random/Choose 选中的分支能力，再执行该效果自身配置的 chainedAbilities。

#### Scenario: Branches execute before effect chained abilities
- **WHEN** Random/Choose 既有 AbilityConfigs 分支列表又配置了 chainedAbilities
- **THEN** 系统 MUST 先执行分支能力
- **AND** 之后按常规流程执行该效果的 chainedAbilities

### Requirement: Decision Branch Results Are Part Of Effect Completion
系统 SHALL 将 Random/Choose 分支能力的执行结果作为该效果本体的执行结果，并在 EffectCompletedEvent 中提供给真正的 chainedAbilities 使用。

#### Scenario: Branch results included in EffectCompletedEvent
- **WHEN** Random/Choose 选中的分支能力执行完成
- **THEN** EffectCompletedEvent.GeneratedEvents 应包含分支能力执行产生的事件
- **AND** 该 EffectCompletedEvent 被用于触发该效果配置的 chainedAbilities
