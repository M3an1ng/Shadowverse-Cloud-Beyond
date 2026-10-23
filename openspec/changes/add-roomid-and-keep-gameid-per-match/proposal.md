# Proposal: add-roomid-and-keep-gameid-per-match

## Why
- 当前多人房间把 `gameId` 同时当作“房间标识”和“单局标识”，语义冲突，导致连战设计困难。
- 现有持久化与结算体系已经把 `gameId` 作为单局唯一键使用（回放、结算、统计），这部分应保留不动以降低风险。
- 本次改造目标是引入独立 `roomId` 管理房间生命周期，同时保留 `gameId` 作为单局生命周期键。

## Scope
- 仅重构“房间标识语义”和“房间 -> 单局映射”。
- 保持 `gameId` 在对局、回放、结算、统计中的语义不变。
- 支持同一 `roomId` 下启动多局（后续 rematch 可直接复用本能力）。

## Non-goals
- 不改动卡牌/战斗规则逻辑。
- 不迁移现有回放和统计表结构。
- 不在本提案内实现完整 UI 重构，仅保证协议兼容迁移路径。

## What Changes
1. 新增 `roomId` 概念，房间接口与连接鉴权以 `roomId` 为主键。
2. `gameId` 保留为单局唯一键，在房间 `Start`（或后续连战确认）时生成。
3. 新增 `gameId -> roomId` 反查能力，供回放鉴权、断线校验、房间状态检查使用。
4. 过渡期保留兼容字段（例如响应体中的旧 `GameId`），避免前端一次性切换风险。

## 逐文件修改清单（Draft）
1. `Server/Domain/Entities/MatchRoom.cs`
- 新增字段：`RoomId`, `ActiveGameId`, `Round`
- 过渡期处理：保留 `GameId`（deprecated，映射 `RoomId`）或移除并全量替换

2. `Server/Services/Abstractions/IMatchService.cs`
- 所有“房间操作”入参从 `gameId` 语义改为 `roomId`
- 新增反查方法：按 `gameId` 查房间

3. `Server/Services/MatchService.cs`
- `_rooms` 字典键改为 `roomId`
- `Create` 生成 `roomId`
- `Start(roomId, ...)` 生成并写入 `ActiveGameId`
- 维护 `gameId -> roomId` 映射（内存）

4. `Server/Controllers/MatchController.cs`
- 路由 `{id}` 语义明确为 `roomId`
- `MatchResponse` 增加 `RoomId`, `ActiveGameId`
- 兼容输出旧字段 `GameId`（短期）

5. `Server/Hubs/GameHub.cs`
- 连接参数优先读取 `roomId`（兼容回退 `gameId`）
- 鉴权与房间状态查询使用 `roomId`
- 动作入队改用 `activeGameId`
- `Context.Items` 增加 `RoomId`、`GameId`（此处 `GameId` 指单局）

6. `Server/Hubs/Filters/GameContextFilter.cs`
- 保持读取 `Context.Items["GameId"]`（单局），无需大改
- 日志文案改为区分 Room/Game（可选）

7. `Server/Hubs/SignalRGroupNames.cs`
- 参数语义从 `gameId` 重命名为 `roomId`（方法名可先不变，避免大面积编译修改）
- 组仍按房间维度广播：`Game_<roomId>`

8. `Server/Services/GameService.cs`
- 单局状态管理继续使用 `gameId` 键（不变）
- `TryGetActiveRoom` 增加 `gameId -> roomId` 反查逻辑

9. `Server/Controllers/ReplayController.cs`
- replay 路由参数继续是 `gameId`（单局）
- `HasReplayReadAccess` 改为：`gameId -> roomId -> joinCode` 校验

10. `Server/Controllers/DevToolsMatchesController.cs`
- 构建响应时改为返回 `RoomId` + `ActiveGameId`
- 内部调用改 `roomId` 语义

11. `Server/Services/IGameService.cs`（可选）
- 保留签名不变，仅统一注释：入参 `gameId` 为单局ID

12. `Server/Domain/Interfaces/IGameStateManager.cs`（可选）
- 注释明确 `gameId` 指单局ID，避免歧义

## 方法签名草案（Draft）
```csharp
// Server/Services/Abstractions/IMatchService.cs
public interface IMatchService
{
    MatchOperationResult Create(Guid? hostPlayerId = null, bool useAiFallback = false, TimeSpan? ttl = null);
    MatchOperationResult TryJoin(string roomId, string joinCode, Guid? guestPlayerId = null);
    MatchOperationResult MarkReady(string roomId, Guid playerId);
    MatchOperationResult SubmitDeckSelection(string roomId, Guid playerId, MatchDeckSelection selection, MatchDeckSelection? aiSelection = null);
    MatchOperationResult Start(string roomId, Guid playerId); // 设置 ActiveGameId
    MatchOperationResult Get(string roomId);
    MatchOperationResult ValidateConnection(string roomId, Guid playerId, string token);
    void Close(string roomId, string reason);
    void CleanupExpired();

    // 新增：反查
    MatchOperationResult GetByGameId(string gameId);
    bool TryGetRoomIdByGameId(string gameId, out string roomId);
}
```

```csharp
// Server/Domain/Entities/MatchRoom.cs
public class MatchRoom
{
    public string RoomId { get; init; } = string.Empty;
    public string? ActiveGameId { get; set; } // 当前进行中的单局
    public int Round { get; set; } = 0;
    // 其余字段保持
}
```

```csharp
// Server/Controllers/MatchController.cs
public class MatchResponse
{
    public string RoomId { get; set; } = string.Empty;
    public string? ActiveGameId { get; set; }

    [Obsolete("Use RoomId instead.")]
    public string? GameId { get; set; }
    // 其余字段保持
}
```

## Migration & Compatibility
- Phase 1（兼容）：后端同时接受 `roomId`/`gameId` 查询参数，响应同时返回 `RoomId` 与旧 `GameId`。
- Phase 2（切换）：前端全部改用 `roomId` 入房、`activeGameId` 入队。
- Phase 3（清理）：删除旧 `GameId`（房间语义）兼容字段和分支。

## Risks
- Hub 连接与断线重连链路涉及点位多，若 room/game 传参混用会引发“找不到对局状态”。
- replay 鉴权若未接入反查，会出现有回放但无房间校验上下文。

## Acceptance Criteria
- 创建房间后返回 `roomId`；开始对局后返回 `activeGameId`。
- 所有对局动作按 `activeGameId` 路由；广播组按 `roomId` 路由。
- 同一 `roomId` 可启动多局且不会触发 `gameId` 相关唯一键冲突。
- replay/结算/统计按 `gameId` 行为与现状一致。
