# Design: Replay Event Journal + Snapshot

## 1. Summary
将“事件历史（EventHistory）”与“录像回放（Replay）/观战（Spectate）”统一在同一份权威数据之上：
- **Journal**：持久化、严格有序、可按范围读取的已提交事件日志（Committed Event Journal）。
- **Snapshot**：用于 seek/快速入场的状态快照存储。
- **EventHistory**：作为 Journal 的运行期缓存/索引视图，而非独立事实来源。

## 2. Key Decisions
1) **记录“已提交事件（facts）”而不是“命令（inputs）”**
- 当前系统以 `ICommittableEvent.Commit` 作为事实状态变更入口；记录 Commit 过程中产生并最终处理的事件序列，可最大化复用既有结算逻辑。

2) **录制格式必须是纯数据（RecordedEvent），不可直接序列化 `IGameEvent`**
- `TargetInfo` 等结构包含运行态引用（对象图），不稳定且难以版本演进；录制格式只允许稳定标识与原子字段（Guid/数值/枚举/字符串）。

3) **以“事务提交包（Committed Bundle）”为写入单位**
- 一次玩家动作事务内会产生多个事件（含连锁/子事件）；Journal 以 bundle 追加写入，提供一致的 `BundleSeq` 与全局单调 `Seq`（用于范围读取与 seek）。

4) **观战与回放共用同一套协议**
- 入场：`snapshot + headSeq`
- 补流：`ReadRange(fromSeq, limit)`
- seek：`GetSnapshotAtOrBefore(targetSeq) + ReadRange(snapshotSeq+1..targetSeq)`

## 3. Data Model (concept)
### 3.1 RecordedEvent
- `GameId` (string)
- `Seq` (long, strictly increasing per game)
- `EventId` (Guid, unique)
- `EventType` (string or int)
- `TimestampUtc` (DateTime)
- `SchemaVersion` (int)
- `PayloadJson` (string, stable fields only)
- `BundleSeq` (long) + `EventIndex` (int)

### 3.2 Snapshot
- `GameId`
- `AtSeq`
- `SchemaVersion`
- `PayloadJson` (DTO / slim snapshot)
- `Compression` (optional)

## 4. Integration Points
- 在事务成功后、广播前写入 bundle（避免出现“广播成功但录像缺失”的缺口）。
- `IEventHistoryService` 仅提供 `EventId -> event` 的查询语义，优先回源 Journal 并缓存。

## 5. Versioning Strategy
- `SchemaVersion` 绑定到 `RecordedEvent.PayloadJson` 结构，任何破坏性变化必须 bump。
- 回放器/rehydrator 必须支持向后兼容（至少支持保留窗口内的版本）。
- 录制元信息可包含 `serverBuild`/`dataHash`，用于定位不兼容录像。

## 6. Performance Notes
- 写入：每 bundle 单次事务、批量插入事件行；索引控制在 range 与 id 查询必需集合。
- 读取：seek 依赖快照密度；默认每回合或每 N bundles 存一次，限制最坏追赶事件数。
- 清理：按保留策略批量删除旧局，避免表无限增长。

