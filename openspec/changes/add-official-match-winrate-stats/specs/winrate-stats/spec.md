## ADDED Requirements

### Requirement: 正式对局判定必须与金币资格一致
系统 MUST 使用与金币发放资格相同的规则判定正式对局，且该判定结果必须作为胜率统计的唯一正式性来源。

#### Scenario: 金币资格为非可发放
- **WHEN** 对局结束结算时金币资格判定为不可发放
- **THEN** 该对局参与者的统计事实 `IsOfficial` MUST 为 `false`
- **AND** 该对局 MUST NOT 参与任何胜率聚合

#### Scenario: 金币资格为可发放
- **WHEN** 对局结束结算时金币资格判定为可发放
- **THEN** 该对局参与者的统计事实 `IsOfficial` MUST 为 `true`
- **AND** 该对局 MUST 参与胜率聚合

### Requirement: 事实表正式性字段最小化
统计事实表 MUST 只保留一个正式性字段 `IsOfficial`，并且 MUST NOT 持久化其他正式性冗余字段。

#### Scenario: 事实表字段约束
- **WHEN** 系统设计并落库统计事实表
- **THEN** 正式性相关字段 MUST 仅包含 `IsOfficial`
- **AND** 系统 MUST NOT 新增 `CoinRewardEligible` 或 `NonOfficialReason` 等冗余字段

### Requirement: 胜率统计仅计算正式对局
系统 MUST 仅基于 `IsOfficial=true` 的对局更新卡牌与卡组胜率聚合。

#### Scenario: 非正式对局不会污染聚合
- **WHEN** 统计结算读取到 `IsOfficial=false` 的对局事实
- **THEN** 系统 MUST 跳过卡牌与卡组聚合更新

#### Scenario: 正式对局更新聚合
- **WHEN** 统计结算读取到 `IsOfficial=true` 的对局事实
- **THEN** 系统 MUST 更新卡牌与卡组聚合计数

### Requirement: 胜率聚合按环境隔离
系统 MUST 按 `DeckEnvironment` 对卡牌与卡组胜率独立聚合。

#### Scenario: 不同环境相同卡组不合并
- **WHEN** 两局对战卡组构成相同但 `DeckEnvironment` 不同
- **THEN** 系统 MUST 将其写入不同环境维度的聚合记录

### Requirement: 玩家级统计按职业聚合
系统 MUST 维护玩家维度的职业统计，并按 `UserId + DeckClass` 聚合游玩场数与胜场数。

#### Scenario: 正式对局更新玩家职业统计
- **WHEN** 统计结算读取到 `IsOfficial=true` 的对局事实
- **THEN** 系统 MUST 按参与者的 `UserId + DeckClass` 更新 `TotalGames`
- **AND** 若该参与者获胜，系统 MUST 同步更新 `TotalWins`

#### Scenario: 非正式对局不更新玩家职业统计
- **WHEN** 统计结算读取到 `IsOfficial=false` 的对局事实
- **THEN** 系统 MUST NOT 更新任何玩家职业聚合记录
