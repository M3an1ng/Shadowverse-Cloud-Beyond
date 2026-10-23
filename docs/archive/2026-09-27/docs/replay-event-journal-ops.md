# Replay Event Journal (Ops)

## Storage (SQLite)
数据默认写入 `Server/Data/shadowverse.db`（见 `Server/appsettings.Development.json`）。

表：
- `ReplayGames`：对局元信息（Created/Ended、ServerBuild、SchemaVersion）。
- `ReplayBundles`：按事务（bundle）记录序列范围（StartSeq..EndSeq）。
- `ReplayEvents`：按事件序列记录（Seq、EventId、EventType、DtosJson）。
- `ReplaySnapshots`：状态快照（AtSeq、Compression、PayloadHash、Payload）。

## Configuration
在 `Server/appsettings*.json` 添加 `Replay` 节点（未配置则使用默认值）：
```json
{
  "Replay": {
    "EnableSnapshots": true,
    "CompressSnapshots": true,
    "SnapshotOnGameStart": true,
    "SnapshotOnTurnStart": true,
    "SnapshotOnGameEnd": true,
    "SnapshotEveryBundles": 0,
    "RetentionDays": 7,
    "CleanupIntervalMinutes": 30,
    "RequireJoinCodeForReplayReads": false
  }
}
```

## APIs
- `GET /api/replay/{gameId}/head`：返回 `HeadSeq` 与最新快照 seq。
- `GET /api/replay/{gameId}/snapshot?atSeq=`：获取 `atSeq` 之前最近的快照。
- `GET /api/replay/{gameId}/events?fromSeq=&limit=`：按 seq 范围拉取事件（含 `ClientDtos`）。
- `GET /api/replay/{gameId}/enter?atSeq=`：返回 `HeadSeq + Snapshot + FromSeq`（便于入场/seek 协议）。
- `GET /api/replay/{gameId}/export`：导出整局 JSON（调试/留档用）。
- `GET /api/replay/{gameId}/validate`：一致性检查（seq 连续性、DTO 可反序列化、最新快照哈希）。

若 `Replay.RequireJoinCodeForReplayReads=true`，上述接口需携带 `?joinCode=XXXXXX`（仅对仍存在的房间强制校验）。

## SignalR (Seek/Catch-up)
Hub：`/gamehub`
- `CatchUp(fromSeq, limit)`：从 Journal 补发事件到当前连接。
- `Seek(targetSeq)`：先发送快照，再补发至 `targetSeq` 的事件。

> 目前这两个方法面向观战/调试使用；前端可在拿到 `snapshot.AtSeq` 后调用 `Seek` 或 `CatchUp` 进行跳转/补流。

## Retention & Cleanup
后台服务 `ReplayCleanupHostedService` 会按 `RetentionDays` 周期性删除过期对局的 replay 数据（events/bundles/snapshots/games）。

## Rate Limiting
`ReplayController` 已启用固定窗口限流策略 `replay-read`（默认 120 req/min）。
