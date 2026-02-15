# Change: Add Official-Match Winrate Statistics

## Why
- 当前缺少稳定的卡牌/卡组胜率统计能力，无法支持环境分组下的长期数据分析。
- 统计口径必须与经济结算口径一致，避免“不能拿金币但进入胜率统计”的规则分叉。
- 需要最小化事实表设计，降低维护复杂度与字段漂移风险。

## What Changes
- 新增胜率统计能力：支持按 `DeckEnvironment` 分组的卡牌与卡组胜率聚合。
- 新增玩家级职业统计：按 `UserId + DeckClass` 记录玩家游玩场数与胜场数（胜率由聚合字段计算）。
- 新增“正式对局”统一判定：复用“对局结束是否可获得金币”的现有规则作为唯一来源。
- 仅正式对局参与胜率统计；非正式对局不更新任何胜率聚合。
- 新增统计事实表（每局每参与者一条）用于幂等结算与离线重算支撑。
- 事实表只保留一个正式性字段：`IsOfficial`（不新增 `CoinRewardEligible`、`NonOfficialReason` 等冗余字段）。

## Impact
- Affected specs: `winrate-stats`
- Affected code (planned):
  - `Server/Services/MetaProgressionService.cs`（抽取/复用正式对局判定）
  - `Server/Services/GameActionTransactionService.cs`（GameEnd 后统计结算入口）
  - `Server/Infrastructure/Data/ApplicationDbContext.cs`（新增统计表映射）
  - `Server/Domain/Entities/*`（新增统计实体）
