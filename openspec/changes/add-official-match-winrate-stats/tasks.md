## 1. Contracts & Rules
- [x] 1.1 抽取共享“正式对局判定”接口，语义与金币资格完全一致。
- [x] 1.2 约束统计入口：仅消费该共享判定，不允许自定义第二套规则。

## 2. Persistence
- [x] 2.1 新增 `GameParticipantStatsFact` 实体与迁移。
- [x] 2.2 在事实表中仅保留 `IsOfficial` 正式性字段，移除/禁止其他正式性冗余字段。
- [x] 2.3 新增 `CardStatsAgg` 与 `DeckStatsAgg` 聚合表与索引。

## 3. Settlement Pipeline
- [x] 3.1 在 `GameEnd` 提交后写入事实表（`GameId + PlayerId` 幂等）。
- [x] 3.2 `IsOfficial=true` 时更新聚合，`IsOfficial=false` 时跳过聚合更新。
- [x] 3.3 聚合维度包含 `DeckEnvironment`，确保跨环境隔离。

## 4. Verification
- [x] 4.1 测试：金币资格为 false 时，事实表 `IsOfficial=false` 且聚合不变。
- [x] 4.2 测试：金币资格为 true 时，事实表 `IsOfficial=true` 且聚合正确累加。
- [x] 4.3 测试：重复结算同一 `GameId + PlayerId` 不重复累计。

## 5. Player Class Stats
- [x] 5.1 新增 `UserClassStatsAgg` 聚合表（`UserId + DeckClass` 唯一）。
- [x] 5.2 在正式对局结算时更新玩家职业维度 `TotalGames/TotalWins`。
- [x] 5.3 测试：非正式对局不更新玩家职业聚合，正式对局与重复结算行为正确。
