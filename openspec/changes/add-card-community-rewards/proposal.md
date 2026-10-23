# Change: Add Card Community Rewards

## Why

当前卡牌详情的社区功能已经支持评论、点赞和点踩，但这些行为与现有金币体系没有联动。为鼓励玩家参与卡牌社区内容，同时避免刷奖励和状态不一致，需要为“同一张卡的首次投票”和“同一张卡的首次评论”提供一次性金币奖励，并将奖励判定纳入后端持久化与事务边界。

## What Changes

- 为卡牌社区新增按卡一次的互动奖励：
  - 玩家对某张卡首次点赞或点踩时获得 `10` 金币。
  - 玩家对某张卡首次发表评论时获得 `50` 金币。
- 奖励资格以“用户 + 卡牌ID + 奖励类型”为准，由后端持久化判定，而不是根据当前评论/投票表的存在状态反推。
- 社区评论与投票写入需要与金币发放保持同一事务，避免出现“社区操作成功但奖励未到账”或“奖励到账但社区操作失败”的状态分裂。
- 复用现有经济系统交易记录，增加社区奖励交易类型和幂等约束，保证同一卡的同类奖励不会重复发放。
- 扩展社区接口响应，使前端能够在首评/首赞踩后展示奖励结果并刷新当前金币显示。
- 明确本次变更不包含认证体系重构；继续沿用现有登录用户上下文与社区接口模型。

## Capabilities

### New Capabilities
- `card-community-rewards`: 为卡牌社区评论与投票提供按卡一次的金币奖励、幂等判定、事务一致性以及前端奖励反馈。

### Modified Capabilities
- None.

## Impact

- Affected backend:
  - `Server/Controllers/CardCommunityController.cs`
  - `Server/Services/IEconomyService.cs`
  - `Server/Services/EconomyService*.cs`
  - `Server/Services/TransactionService.cs`
  - `Server/Domain/Entities/CoinTransaction.cs`
  - `Server/Infrastructure/Data/ApplicationDbContext.cs`
  - `Server/Infrastructure/Persistence/Migrations/*`
- Affected frontend:
  - `src/components/CardDetailMockup.js`
  - `src/main.js`
- Affected APIs:
  - `POST /api/cards/{cardId}/community/comments`
  - `PUT /api/cards/{cardId}/community/vote`
  - Potentially shared economy refresh flows driven by community actions
- Data model impact:
  - Add new coin transaction types for community rewards
  - Add idempotency-safe reward identity keyed by user, reward type, and card definition
