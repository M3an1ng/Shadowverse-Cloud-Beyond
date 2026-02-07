## 1. Design
- [x] 1.1 定义 `RecordedEvent`/`CommittedBundle`/`Snapshot` 数据模型与版本化策略（schemaVersion + serverBuild + dataHash）。
- [x] 1.2 定义接口：`ICommittedEventJournal`、`ISnapshotStore`、`IRecordedEventMapper`、`IGameEventRehydrator`（或 `IRecordedEventApplier`）。
- [x] 1.2.1（MVP）定义最小录制接口 `ICommittedEventJournal`（append + range read + head seq，用于 debug 导出与后续补流）。
- [x] 1.3 明确一致性策略：journal 写入失败时的事务处理策略（对局优先 vs 录像完整优先）与可选 outbox 路线。（当前采用“对局优先”：写入失败仅记录日志，不中断对局）
- [x] 1.4 明确 seek 策略：快照频率默认值与可配置项（每回合 / 每 N bundles），以及观战入场协议（snapshot + fromSeq）。
- [x] 1.5 明确保留/清理策略：按天数/数量/类型分层保留；清理任务触发时机与幂等保证。

## 2. Storage (SQLite first)
- [x] 2.1 新增 EF Core 实体与迁移：`ReplayGames`、`ReplayBundles`、`ReplayEvents`、`ReplaySnapshots`（或等价结构）。
- [x] 2.1.1（MVP）新增 `ReplayEvents` 实体与迁移（SQLite）。
- [x] 2.2 设计索引：`(GameId, Seq)` range 主索引；`EventId` unique；`Snapshots(GameId, AtSeq)`。（已落地 `ReplayEvents` 的 `(GameId, Seq)` 与 `EventId` unique）
- [x] 2.3 实现 `ICommittedEventJournal` 的 SQLite 版本（批量写入：每 bundle 单次事务/SaveChanges）。
- [x] 2.3.1（MVP）实现 `SqliteReplayJournal`：批量写入 `ReplayEvents`，并支持 `ReadRange`/`HeadSeq` 查询。
- [x] 2.4 实现 `ISnapshotStore` 的 SQLite 版本（支持压缩/不压缩两种配置）。

## 3. Mapping & Replay Core
- [x] 3.1 实现 `IRecordedEventMapper`：将客户端事件 DTO 映射为可持久化的 `RecordedEvent`（仅存稳定字段与 JSON）。
- [x] 3.1.1（MVP）最小录制映射：EventId/EventType/Timestamp/Announcement/SourceId/TargetIds。
- [x] 3.2 实现 `IGameEventRehydrator`：`RecordedEvent -> IGameEvent`（best-effort，用于 EventHistory miss 回源）。
- [ ] 3.3 建立“回放器”服务：从 snapshot/初始状态构建 `GameState`，按 seq 回放并调用既有 `Commit`（或专用 Applier）。
- [x] 3.3.1（实现）提供面向观战/调试的回放链路：`GameHub.Seek(targetSeq)` + `GameHub.CatchUp(fromSeq)`，复用既有 SignalR 事件处理。
- [ ] 3.4 引入最小验证：回放后状态哈希与在线对局一致（抽样字段/快照比对）。

## 4. Integration
- [x] 4.1 在 `CommitTransactionAsync` 成功后、广播前追加写入 bundle（保证 Journal 与广播顺序一致）。
- [x] 4.2 将 `IEventHistoryService` 改造为 Journal 的缓存/装饰器：按 `EventId` miss 则回源 Journal。
- [x] 4.3 对游戏结束/房间关闭补齐清理钩子：清理 EventHistory 缓存、可选写最终快照与标记完成。

## 5. Spectate & Replay APIs
- [x] 5.1 定义观战入场接口：获取 `latestSeq` + 最新 snapshot（或指定 seq 之前 snapshot）。
- [x] 5.1.1（MVP）观战连接：`?spectate=1&gameId=<id>` 加入组并单播 `ReceiveGameState`。
- [x] 5.2 定义补流接口：按 `fromSeq` 拉取事件范围（分页/limit）。（已提供 `GET /api/replay/{gameId}/events?fromSeq&limit`）
- [x] 5.3 定义录像查询接口：按 gameId 获取元信息、按 seq 范围拉取、按时间/玩家查询（后续可扩展）。（已提供 `events` + `export`）
- [x] 5.4 权限与速率限制：观战/回放读取的鉴权与限流（最小可用实现即可）。

## 6. Observability & Ops
- [x] 6.1 指标：bundle 写入耗时、事件数、payload 大小、快照大小、清理删除量、回放耗时。
- [x] 6.2 清理任务：后台定时作业（保留天数可配），支持 dry-run 日志与幂等。
- [x] 6.3 文档：开发期运维指南（数据目录、保留策略、常见故障与恢复）。

## 7. Validation
- [x] 7.1 单测：mapper/rehydrator（关键事件类型）与 journal range/id 查询。
- [x] 7.1.1（MVP）更新现有测试以适配事务服务新增依赖（不新增 replay 专项测试）。
- [ ] 7.2 集成：一局对战产生的 bundle 可写入并可回放到同样的终局状态（快照/哈希校验）。
- [ ] 7.3 手动：观战入场后可追上实时事件；断线后从 fromSeq 补齐无缺口。
