## Context

当前经济与任务相关逻辑已经存在于后端，但职责边界不够适合继续扩展用户行为任务：

- `MetaProgressionService` 在官方对局结算后直接发放对局金币，并推进 `PlayGames` / `WinGames` 任务。
- `CardCommunityService` 在评论和首次投票成功后直接调用 `IEconomyService.AddCoinsAsync` 发放社区金币。
- `EconomyService.Quests` 已支持任务列表、进度更新和任务领奖，但 `UserQuest` 当前以 `(UserId, QuestDefinitionId)` 唯一，无法自然表达每日/周常周期。
- 前端卡牌详情会把社区响应中的 `rewardGranted`、`rewardCoins`、`currentCoins` 当作即时金币奖励来展示。

本变更将对局直接金币与其他用户行为奖励分离：官方对局结算继续直接发金币，评论、点赞/踩、登录、观战等行为只推进任务进度，金币统一从任务领奖路径进入账户。

## Goals / Non-Goals

**Goals:**

- 支持多种用户行为任务进度来源：对战、胜利、评论、点赞/踩、登录、观战。
- 支持每日、周常、特殊、成就等任务周期，避免同一任务定义只能完成一次。
- 支持按来源实体去重，防止同一张卡反复赞/踩、同一场对局重复结算、同一天多次登录刷进度。
- 让非对局行为不再直接发金币，任务奖励通过 `QuestCompletion` 流水统一发放。
- 保留历史社区金币流水类型的显示兼容，不删除旧记录。
- 让前端获得任务进度更新和完成提示，替代社区即时金币奖励动画。

**Non-Goals:**

- 不引入任务 DSL 或复杂条件表达式系统。
- 不重构认证体系或修复现有接口信任前端 `userId` 的历史问题。
- 不改变官方对局结算金币奖励资格判定。
- 不实现任务随机池、任务替换、付费刷新或活动后台配置 UI。
- 不迁移或冲正历史社区奖励金币流水。

## Decisions

### 1) Add a dedicated `QuestProgressService`

新增 `IQuestProgressService` 作为所有行为任务进度的唯一入口：

```csharp
Task<QuestProgressResult> RecordActivityAsync(
    Guid userId,
    QuestActivityType activityType,
    string sourceEntityType,
    string sourceEntityId,
    int increment = 1,
    DateTime? occurredAtUtc = null,
    CancellationToken cancellationToken = default);
```

业务服务只在行为成功后上报标准化 activity，不直接查询或修改 `UserQuest`。`QuestProgressService` 负责：

- 查找匹配 `QuestActivityType` 的激活任务定义。
- 解析当前任务周期。
- 创建或获取对应 `UserQuest`。
- 应用去重策略。
- 更新进度并返回更新/完成结果。

**Alternative considered:** 继续扩展 `EconomyService.UpdateQuestProgressAsync(Guid, QuestType, int)`。  
**Reason rejected:** 当前接口无法携带来源实体和去重上下文，继续扩展会让 `EconomyService` 同时承担金币、任务匹配、行为去重和周期管理，职责过大。

### 2) Separate activity type from quest display type

新增 `QuestActivityType` 表达行为事件源：

- `PlayMatch`
- `WinMatch`
- `Comment`
- `Vote`
- `Login`
- `SpectateMatch`

`QuestDefinition` 增加 `ActivityType`。现有 `QuestType` 可以保留用于兼容 DTO 或展示，但新进度匹配应以 `ActivityType` 为准。

**Alternative considered:** 直接在现有 `QuestType` 中继续追加 `VoteCards`、`CommentCards`、`SpectateGames`。  
**Reason rejected:** `QuestType` 目前兼具展示与任务分类语义，直接作为事件类型会限制后续“同一行为支持多个任务描述”的扩展。

### 3) Periodize `UserQuest`

将用户任务唯一性从 `(UserId, QuestDefinitionId)` 改为 `(UserId, QuestDefinitionId, PeriodKey)`，并新增：

- `PeriodKey`
- `PeriodStartUtc`
- `PeriodEndUtc`

周期规则由 `QuestCategory` 和 `RefreshIntervalHours` 派生：

- Daily：按 UTC 日期生成 `yyyy-MM-dd`。
- Weekly：按 ISO week 或明确周起始日生成 `yyyy-Www`。
- Special：按定义的刷新窗口生成稳定 key。
- Achievement：使用稳定 key，例如 `achievement`。

**Alternative considered:** 到期后重置同一条 `UserQuest`。  
**Reason rejected:** 重置会丢失历史完成和领奖状态，也难以审计重复领奖与周期行为。

### 4) Add quest progress event records for deduplication

新增 `QuestProgressEvent` 表，字段包括：

- `Id`
- `UserId`
- `QuestDefinitionId`
- `ActivityType`
- `PeriodKey`
- `SourceEntityType`
- `SourceEntityId`
- `Increment`
- `CreatedAtUtc`

唯一索引：

```text
(UserId, QuestDefinitionId, PeriodKey, SourceEntityType, SourceEntityId)
```

`QuestDefinition` 增加 `DeduplicationPolicy`：

- `None`
- `PerSourceEntityPerPeriod`
- `OncePerPeriod`

对赞/踩 10 张不同卡，`SourceEntityType = Card`、`SourceEntityId = cardId`。对登录，`SourceEntityType = Login`、`SourceEntityId = PeriodKey`。对对局，`SourceEntityType = Match`、`SourceEntityId = gameId`。

**Alternative considered:** 直接从 `CardVotes`、`CardComments`、`GameSettlements` 等业务表推导进度。  
**Reason rejected:** 投票可取消、评论可删除，当前业务表只表达当前状态，不能可靠表达“本周期是否已计入任务”。

### 5) Keep match coins direct; convert miscellaneous coins to task rewards

`MetaProgressionService` 保留官方对局金币直接发放，因为它是对局结算的一部分，并已有 `GameSettlements` 幂等边界。对局结算同时调用 `QuestProgressService.RecordActivityAsync` 推进对战/胜利任务。

`CardCommunityService` 移除社区直接奖励调用，评论和投票成功后调用 `QuestProgressService.RecordActivityAsync`。旧的 `CoinTransactionType.CardCommunityFirstVote` 和 `CardCommunityFirstComment` 保留在枚举和显示名称中，仅用于历史流水。

### 6) Change activity responses to return quest progress metadata

社区评论/投票响应新增或替换为任务进度摘要：

```json
{
  "questProgress": {
    "updatedQuestIds": [],
    "completedQuestIds": [],
    "claimableCount": 0
  }
}
```

前端收到后显示“任务进度 +1”“任务已完成，可领取奖励”，不再触发社区金币到账动画。金币数字只在任务领奖成功后刷新。

## Risks / Trade-offs

- [Risk] 当前接口仍信任前端传入 `userId`，任务奖励会放大刷号和冒领风险。  
  → Mitigation：本变更不重构认证，但所有任务进度和领奖必须继续校验用户存在；后续应以认证上下文替代请求体/路由中的 userId。

- [Risk] 嵌套事务和同一 DbContext 内多服务协作可能导致保存时机混乱。  
  → Mitigation：`QuestProgressService` 使用现有 `ITransactionService`，并依赖其 CurrentTransaction 检测；业务服务在同一事务内先完成业务写入，再记录任务事件。

- [Risk] SQLite 与 PostgreSQL 对 filtered unique index 的能力不同。  
  → Mitigation：任务进度事件使用普通唯一索引，不依赖 provider-specific filter；可空字段需要通过默认值归一化避免唯一索引漏判。

- [Risk] 社区前端现有即时金币动画会失效。  
  → Mitigation：用任务进度反馈替换，任务领奖成功后再使用现有顶部金币增长动画。

- [Risk] 任务定义缺少初始化数据会导致行为记录没有可更新任务。  
  → Mitigation：迁移或启动种子数据应提供首批 daily quest definitions，并允许无匹配任务时返回空进度结果而不影响原业务操作。

## Migration Plan

1. 新增 `QuestActivityType`、`QuestDeduplicationPolicy` 和任务进度结果 DTO。
2. 扩展 `QuestDefinition`，新增活动类型和去重策略字段，并为现有对局任务填充映射。
3. 扩展 `UserQuest` 周期字段，迁移现有用户任务到稳定 period key。
4. 新增 `QuestProgressEvents` 表和唯一索引。
5. 新增 `QuestProgressService`，并让现有 `EconomyService.UpdateQuestProgressAsync` 逐步转接或保留兼容。
6. 将 `MetaProgressionService` 对战任务推进改为 activity 记录。
7. 将 `CardCommunityService` 社区直接金币奖励改为 activity 记录。
8. 接入登录和观战入口的 activity 记录。
9. 更新 API/DTO 和前端反馈。
10. 更新测试并执行后端任务/社区/对局相关测试。

Rollback 策略：保留历史金币流水枚举和现有任务领奖路径；若新任务进度记录出现问题，可临时关闭新增任务定义的 `IsActive`，对局直接金币不受影响。

## Open Questions

- 每日/周常周期使用 UTC 还是玩家本地时区。建议第一版统一 UTC，避免用户时区字段缺失导致规则不一致。
- 观战“有效行为”的判定门槛。建议第一版使用“同一用户同一场观战只计一次”，后续再增加观战时长或看到结算的条件。
- 登录 activity 应由认证成功触发，还是由主菜单拉取元进度时触发。建议第一版接入认证成功路径；开发环境可保留测试端点。
