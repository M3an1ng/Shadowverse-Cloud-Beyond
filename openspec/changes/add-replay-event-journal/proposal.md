# Change: 构建可持久化事件日志（Event Journal）以支持观战与录像回放

## Why
- 当前 `IEventHistoryService` 仅为进程内事件回查（按 `EventId` 索引），不持久化、不保序，无法作为录像与观战的可靠基础。
- 录像回放与观战本质都依赖“可 seek 的有序事件流 + 可快速入场的快照”，需要统一的长期维护架构以避免重复实现与数据不一致。
- 事件对象当前包含运行态引用（如 `TargetInfo` 持有 `CardInstance/PlayerState`），无法作为稳定的录制格式；需要引入版本化的“录制事件”数据模型。

## What Changes
- 引入权威的 **Committed Event Journal**：以“事务提交包（Committed Bundle）”为单位追加写入，产生全局单调 `Seq`，可按范围读取与按 `EventId` 回查。
- 引入 **Snapshot Store**：定期保存可序列化的游戏快照（DTO/瘦身模型），支持观战入场与录像跳转（seek）。
- 将现有 `EventHistory` 定位为 **Journal 的缓存/索引视图（read model）**：优先复用 Journal 数据，减少双写与分叉逻辑。
- 为“观战/回放”定义稳定的数据协议与接口（仅后端基础设施与 API/Hub 合约，不包含完整 UI）。
- 约定错误策略、版本演进策略、保留/清理策略与指标，确保长期运维可控。

## Impact
- Affected specs: replay-event-journal
- Affected code (planned): 事务提交管线（`GameActionTransactionService`）、事件模型录制映射/反序列化、EF Core 数据表与迁移、Hub/Controller 增补观战/回放接口、后台清理与监控。

