## ADDED Requirements
### Requirement: 稳定的用户身份与对局参与者绑定
系统 MUST 能将对局参与者稳定绑定到持久化 `User.Id`，使对局结算、经济与任务推进能够可靠落库。

#### Scenario: 用户对局使用稳定 UserId 作为 PlayerId
- **WHEN** 创建一局由真实用户参与的对局
- **THEN** 对局内使用的 `playerId` MUST 等于对应用户的 `User.Id`

#### Scenario: AI/游客参与者不写入用户元进度
- **WHEN** 对局参与者为 AI 或非持久化身份
- **THEN** 系统 MUST NOT 为该参与者写入用户经济/任务/排行/资料数据

### Requirement: 无歧义的数据模型与关系约束
经济、任务、排行、每日奖励与会话记录 MUST 以 `UserId` 作为唯一锚点建立关系，且数据模型 MUST NOT 存在“同一列同时指向多个聚合”的歧义外键。

#### Scenario: 任务记录通过 UserId 关联用户
- **WHEN** 系统持久化用户任务进度
- **THEN** `UserQuest` MUST 通过 `UserId -> Users(Id)` 关联用户，并通过 `QuestDefinitionId` 关联任务定义

#### Scenario: 排行与每日奖励通过 UserId 关联用户
- **WHEN** 系统持久化用户排行或每日奖励
- **THEN** `UserRanking` 与 `UserDailyReward` MUST 通过 `UserId -> Users(Id)` 关联用户，且该关系具备唯一约束

### Requirement: 对局结算的事务性与幂等性
系统 MUST 在对局结算时以事务方式更新用户元进度，并保证幂等：同一局结算被重复触发时不得重复发奖或重复累计统计。

#### Scenario: 重复触发结算不会重复发放金币
- **WHEN** 系统对同一 `GameId` 重复执行结算流程
- **THEN** 用户金币流水与余额变更 MUST 至多发生一次（幂等）

#### Scenario: 结算失败不污染用户数据
- **WHEN** 结算流程中发生数据库错误导致事务回滚
- **THEN** 系统 MUST 保证用户余额/流水/任务状态保持结算前一致，并记录可诊断日志

### Requirement: 任务推进与奖励语义一致
系统 MUST 将“任务完成”与“任务奖励领取”定义为一致且可维护的语义，并提供可审计的奖励发放记录。

#### Scenario: 任务完成后可领取奖励
- **WHEN** 用户任务进度达到 `QuestDefinition.TargetValue`
- **THEN** 任务 MUST 标记为完成，且用户可通过领取流程获得奖励

#### Scenario: 领取奖励至多一次
- **WHEN** 用户尝试对同一已完成任务重复领取奖励
- **THEN** 系统 MUST 拒绝重复领取，且不会重复产生金币流水

### Requirement: 任务进度可由对局事件流推进
系统 MUST 能基于对局已提交事件流推进任务进度，以最大程度复用主游戏系统的事件驱动架构。

#### Scenario: 胜利/对局数任务由结算推进
- **WHEN** 一局对战结束并产生胜负结果
- **THEN** 系统 MUST 推进 `WinGames` 与 `PlayGames` 等任务的进度

#### Scenario: 行为类任务由领域事件推进
- **WHEN** 对局内产生可映射的领域事件（如出牌、召唤、伤害、进化）
- **THEN** 系统 MUST 将其投影为任务进度增量并落库

### Requirement: 状态更新可被客户端高效获取
系统 MUST 提供查询元进度状态的接口，并在必要时支持服务端主动推送更新以减少轮询。

#### Scenario: 客户端查询金币/任务/排行
- **WHEN** 客户端请求用户元进度状态
- **THEN** 系统返回包含金币、任务列表、排行与每日奖励状态的结果

#### Scenario: 对局结算后客户端收到更新
- **WHEN** 对局结算完成并成功落库
- **THEN** 系统可向该用户推送经济/任务更新事件或提供可立即查询到的一致状态

