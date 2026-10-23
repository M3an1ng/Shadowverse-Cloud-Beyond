## Context

卡牌社区功能当前已经支持按卡牌 ID 共享的评论、点赞和点踩，但写操作直接位于 `CardCommunityController`，评论和投票各自直接落库。金币系统则通过 `IEconomyService` 与 `CoinTransaction` 记录管理，并由 `TransactionService` 提供事务包装。新增“首次评论/首次投票奖励”后，社区功能将首次跨越社区数据与经济数据两条写入路径。

当前社区行为还包含两个对奖励判定有影响的业务规则：
- 点同一投票按钮会取消投票，因此 `CardVotes` 表表示的是“当前投票状态”，不是完整历史。
- 评论只允许新增和删除，因此 `CardComments` 表也不能可靠表示“是否曾经首评过该卡”。

因此，奖励资格不能从当前评论/投票表是否存在来反推，必须有独立的持久化奖励事实。同时，本次变更明确不处理认证体系重构，只在现有登录用户模型上补全奖励逻辑。

## Goals / Non-Goals

**Goals:**
- 为同一卡的首次投票提供一次性 `10` 金币奖励。
- 为同一卡的首次评论提供一次性 `50` 金币奖励。
- 确保奖励资格以用户、卡牌和奖励类型为粒度稳定持久化，不受评论删除和投票取消影响。
- 确保社区写操作与金币奖励保持同一事务边界，避免部分成功。
- 让前端在社区操作成功后获得奖励结果和最新金币，及时刷新显示。

**Non-Goals:**
- 不重构登录、鉴权或会话信任模型。
- 不调整现有评论/投票业务规则，如“再次点击同按钮取消投票”“评论不可编辑”。
- 不引入新的经济货币或奖励配置系统；奖励值固定为 `10` / `50`。
- 不新增通用通知中心；前端仅处理当前卡牌详情内的奖励反馈和金币刷新。

## Decisions

### 1) Reward identity reuses `CoinTransaction` instead of adding a dedicated reward table

采用现有 `CoinTransaction` 作为奖励发放事实载体，新增两个交易类型：
- `CardCommunityFirstVote`
- `CardCommunityFirstComment`

并使用 `RelatedEntityId` 持久化奖励身份，例如：
- `card-community:first-vote:{cardDefinitionId}`
- `card-community:first-comment:{cardDefinitionId}`

这样可以复用现有经济流水、余额更新与历史查询能力，避免为一次性奖励再新增单独表与读写路径。

备选方案：
- 新增 `CardCommunityRewardGrants` 表单独记录资格。  
  放弃原因：模型更纯，但会复制现有交易记录能力，并引入额外关联与迁移成本。

### 2) Reward eligibility is based on persisted grant identity, not current community rows

首次奖励资格一律通过是否已存在对应的奖励流水判断，而不是根据 `CardVotes` 或 `CardComments` 当前是否有记录判断。

原因：
- 取消投票会删除当前投票记录。
- 删除评论会移除当前评论记录。
- 如果依赖当前状态，用户可以通过取消或删除再次触发奖励。

### 3) Community write and reward grant are orchestrated in a dedicated service

新增 `CardCommunityService` 负责：
- 评论创建
- 投票变更
- 首次奖励判定
- 金币写入
- 响应 DTO 组装

控制器只保留请求校验和 HTTP 响应映射。这样可以把跨社区与经济系统的事务协调收敛到一个地方，避免继续扩张 `CardCommunityController`。

备选方案：
- 继续在 `CardCommunityController` 中直接注入 `IEconomyService` 和 `ITransactionService`。  
  放弃原因：最小改动但职责继续堆叠，后续再加社区规则会快速失控。

### 4) Rewarded actions run inside one transaction boundary

`CardCommunityService` 使用现有 `ITransactionService` 包裹以下组合操作：
- 首次评论：写 `CardComment` + 写金币流水 + 更新 `UserEconomy`
- 首次投票：写/改 `CardVote` + 写金币流水 + 更新 `UserEconomy`

当动作不满足奖励资格时，仍然通过同一服务执行，但只落社区状态，不落奖励流水。

这样可以保证：
- 首次动作成功但奖励失败时整体回滚
- 并发下奖励资格检查与流水写入尽量靠近，降低重复发奖窗口

### 5) Idempotency is enforced by database uniqueness for reward-grant transactions

在 `CoinTransactions` 上为社区奖励类型补充唯一约束，约束键为：
- `UserId`
- `Type`
- `RelatedEntityId`

只对社区奖励相关的交易类型生效。这样在并发或重试场景下，即便业务层存在重复请求，也能保证同一卡的同类奖励最多插入一次。

备选方案：
- 只在服务层先查后插，不做数据库唯一约束。  
  放弃原因：并发下仍然可能双发，数据库层兜底更稳。

### 6) Community APIs return reward metadata and current coin balance

扩展评论创建与投票响应 DTO，增加以下字段：
- `rewardGranted`
- `rewardCoins`
- `currentCoins`

前端据此：
- 在卡牌详情内显示“首次评论 +50 金币”或“首次投票 +10 金币”
- 刷新顶部金币显示，而不是等待下次轮询或页面重进

备选方案：
- 社区操作后前端再主动调用一次 `/api/economy/coins/{userId}`。  
  放弃原因：请求更多，且奖励提示与余额刷新会分裂成两次状态更新。

## Risks / Trade-offs

- [风险] 现有接口仍然信任前端传入 `userId`，发金币后安全风险被放大。  
  → Mitigation：本次设计显式标记为非目标；实现时保留后续接入统一鉴权的扩展点，不把用户解析逻辑继续散落到更多地方。

- [风险] `CoinTransactionType` 新增值会影响已有显示逻辑和历史列表。  
  → Mitigation：同步更新类型显示名称，并验证金币历史接口能正确展示社区奖励。

- [风险] 迁移路径在仓库内存在目录不完全统一的情况。  
  → Mitigation：在实施前确认当前有效 EF Core migration 目录，并将新迁移落到同一链路中。

- [风险] 社区服务新增后，控制器、经济服务、前端 DTO 都要联动，改动面跨模块。  
  → Mitigation：保持范围收敛，只处理首评/首赞踩奖励，不顺带重构认证或社区其他规则。

## Migration Plan

1. 新增社区奖励交易类型，并更新交易类型显示名称。
2. 新增 `CardCommunityService` 及其接口，封装评论/投票与奖励协调逻辑。
3. 调整 `CardCommunityController` 使用社区服务返回统一结果。
4. 新增数据库迁移，为社区奖励交易建立唯一约束。
5. 扩展社区响应 DTO，返回奖励结果和最新金币余额。
6. 前端卡牌详情接入奖励提示和金币刷新。
7. 验证首评、首赞踩、重复操作、取消投票、删除评论、并发重复请求等关键场景。

## Open Questions

- 社区奖励是否需要在金币历史中显示为独立中文名称，还是沿用通用“其他”分类不可接受。
- 首次奖励提示是否只在卡牌详情内显示，还是未来要接入全局 toast。
- 当前是否接受“先不修鉴权，只做功能落地”，还是要把用户身份可信度问题与本变更一起处理。
