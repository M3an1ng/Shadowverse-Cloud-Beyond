# M5: SignalR 实时通信设计文档 V1.2

## 1. 概述 (Overview)

本文档旨在详细设计 M5 里程碑中引入的 SignalR 实时通信机制。目标是替换原有的基于轮询或简单 HTTP 请求的游戏状态同步方式，实现更高效、更实时的双向通信。根据最终确认的规划，此设计将涵盖：

1.  **服务器到客户端 (Server-to-Client) 的状态推送：** 使用 SignalR 将游戏状态变化（如回合更迭、单位受伤、卡牌抽取等）实时推送给所有相关客户端。
2.  **客户端到服务器 (Client-to-Server) 的动作发起：** 玩家的核心游戏操作（如出牌、攻击、进化、结束回合）将通过客户端调用 SignalR Hub 的方法来发起，取代原有的 HTTP API 调用，以更好地处理潜在的触发效果和交互。
3.  **游戏分组 (Grouping):** 利用 SignalR Group 功能，确保每个游戏实例的消息只发送给该局游戏的参与者。
4.  **平台无关性:** 消息载荷 (Payload) 将使用平台无关的 DTO (Data Transfer Objects) 格式（主要为 JSON），便于未来 Unity 等其他客户端接入。

**参考:**

- [文档/技术文档/后端架构设计\_V1.0.md](文档/技术文档/后端架构设计_V1.0.md) (确认 SignalR 在架构中的位置)
- [文档/项目整体任务清单.md](文档/项目整体任务清单.md) (M5 具体任务项)
- [文档/设计文档/核心玩法/完整游戏流程图.md](文档/设计文档/核心玩法/完整游戏流程图.md) (回合开始流程依据)

## 2. GameHub 接口定义 (Interface Definition)

`GameHub` 是 SignalR 通信的核心。为了清晰和类型安全，我们使用强类型 Hub。

### 2.1 服务器 -&gt; 客户端方法 (Server-to-Client Methods)

定义一个 C# 接口 `IGameClient`，包含所有服务器可以调用客户端的方法。

```csharp
// 命名空间: ShadowverseBeyond.Server.Hubs.ClientInterfaces
using ShadowverseBeyond.Server.Dtos; // Ensure DTO namespace is imported
using System.Threading.Tasks;

public interface IGameClient
{
    // 核心状态更新
    Task ReceiveGameState(GameStateDto gameState); // 推送完整的游戏状态 (可能较少使用，主要用于初始加载或重连)
    Task ReceiveGameEvent(GameEventDto gameEvent); // 推送具体的游戏事件 (更常用)
    Task ReceiveError(string errorMessage);       // 推送游戏逻辑错误或警告信息

    // 特殊交互请求
    Task RequestTargetSelection(TargetSelectionRequestDto request); // 请求客户端选择目标
    // ... 其他可能的交互，如抉择 (Choose One) 等
}
```

**关键方法说明:**

- `ReceiveGameState`: 用于发送完整的游戏状态快照。适用于游戏开始、玩家重连等场景。数据量可能较大。
- `ReceiveGameEvent`: 这是最常用的方法，用于推送增量的游戏事件，如卡牌抽取、单位受伤、PP/EP 变化、回合结束等。客户端根据事件类型更新本地状态。使用基类 `GameEventDto` 并利用多态性处理不同的事件类型。
- `ReceiveError`: 用于向特定客户端发送游戏规则相关的错误或提示信息（例如，“PP 不足”、“目标无效”）。
- `RequestTargetSelection`: 当服务器需要玩家指定一个或多个目标时（如法术指定目标、攻击目标选择），调用此方法。DTO 包含选择的上下文信息（如可选目标列表、数量要求等）。

### 2.2 客户端 -&gt; 服务器方法 (Client-to-Server Methods)

这些方法定义在 `GameHub` 类中，由客户端调用以执行游戏动作。

```csharp
// 文件: ShadowverseBeyond.Server.Hubs.GameHub.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Hubs.ClientInterfaces; // IGameClient
using ShadowverseBeyond.Server.Services;            // IGameService
using ShadowverseBeyond.Server.Dtos.SignalR;        // DTOs (Ensure correct namespace)
using System;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Exceptions; // GameLogicException
using System.Collections.Generic; // List<>

namespace ShadowverseBeyond.Server.Hubs
{
    // 使用强类型 Hub，泛型参数为客户端接口定义
    public class GameHub : Hub<IGameClient>
    {
        private readonly IGameService _gameService;
        private readonly ILogger<GameHub> _logger;

        public GameHub(IGameService gameService, ILogger<GameHub> logger)
        {
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // --- 核心游戏动作 ---

        /// <summary>
        /// 客户端调用此方法来请求执行“出牌”动作。
        /// </summary>
        /// <param name="actionDto">包含出牌动作所需参数的 DTO。</param>
        /// <returns>一个 Task，表示异步操作。</returns>
        public async Task PerformPlayCard(PlayCardActionDto actionDto)
        {
            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync();
            if (gameId == Guid.Empty || string.IsNullOrEmpty(playerId)) return; // Early exit if context is invalid

            try
            {
                _logger.LogInformation("Player {PlayerId} in Game {GameId} attempting to play card {CardUid}",
                    playerId, gameId, actionDto.CardUidInHand);

                // 调用核心游戏逻辑服务处理出牌动作
                await _gameService.PlayCardAsync(gameId.ToString(), playerId, actionDto);

                // 注意：游戏服务内部应负责通过 IHubContext<GameHub, IGameClient> 将产生的事件
                // (如 UnitPlayedEvent, PpUpdateEvent 等) 推送给组内的所有客户端。
                // Hub 本身在这里通常不直接发送事件，除非是简单的确认或错误。
                _logger.LogInformation("Player {PlayerId} successfully initiated playing card {CardUid} in Game {GameId}",
                    playerId, gameId, actionDto.CardUidInHand);

                // 可选：如果需要立即给调用者一个快速反馈，可以发送一个简单的确认
                // await Clients.Caller.ReceiveSimpleAck("PlayCardInitiated");
            }
            catch (GameLogicException ex)
            {
                // 捕获游戏逻辑层抛出的特定异常
                _logger.LogWarning("Game Logic Error for Player {PlayerId} in Game {GameId} playing card {CardUid}: {Message}",
                    playerId, gameId, actionDto.CardUidInHand, ex.Message);
                // 将错误信息仅发送给发起操作的客户端
                await Clients.Caller.ReceiveError($"Play Card Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                // 捕获其他意外错误
                _logger.LogError(ex, "Unexpected error processing PlayCard for Player {PlayerId} in Game {GameId}, Card {CardUid}",
                    playerId, gameId, actionDto.CardUidInHand);
                // 向调用者发送通用错误信息
                await Clients.Caller.ReceiveError("An unexpected server error occurred while playing the card.");
                // 可考虑是否需要通知管理员或进行更详细的日志记录
            }
        }


        /// <summary>
        /// 客户端调用此方法结束自己的回合。
        /// </summary>
        public async Task EndTurn()
        {
            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync();
            if (gameId == Guid.Empty || string.IsNullOrEmpty(playerId)) return;

            try
            {
                _logger.LogInformation("Player {PlayerId} ending turn in Game {GameId}", playerId, gameId);
                // 调用核心服务处理回合结束逻辑
                await _gameService.EndTurnAsync(gameId.ToString(), playerId);
                // 游戏服务应负责推送回合切换事件、PP 刷新事件、抽卡事件等
                _logger.LogInformation("Player {PlayerId} successfully initiated ending turn in Game {GameId}", playerId, gameId);
            }
            catch (GameLogicException ex)
            {
                 _logger.LogWarning("Game Logic Error for Player {PlayerId} in Game {GameId} ending turn: {Message}", playerId, gameId, ex.Message);
                 await Clients.Caller.ReceiveError($"End Turn Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Unexpected error processing EndTurn for Player {PlayerId} in Game {GameId}", playerId, gameId);
                 await Clients.Caller.ReceiveError("An unexpected server error occurred while ending the turn.");
            }
        }

        // ... 其他动作方法，如 PerformAttack, PerformEvolve 等 ...
/// <summary>
        /// 客户端调用此方法来请求执行“单位进化”动作。
        /// </summary>
        /// <param name="actionDto">包含进化动作所需参数的 DTO (要进化的单位 ID)。</param>
        public async Task PerformEvolve(EvolveActionDto actionDto)
        {
            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync();
            if (gameId == Guid.Empty || string.IsNullOrEmpty(playerId)) return;

            try
            {
                _logger.LogInformation("Player {PlayerId} in Game {GameId} attempting to evolve unit {UnitId}",
                    playerId, gameId, actionDto.UnitId);

                // 调用核心游戏逻辑服务处理进化动作
                await _gameService.EvolveAsync(gameId.ToString(), actionDto);

                _logger.LogInformation("Player {PlayerId} successfully initiated evolving unit {UnitId} in Game {GameId}",
                    playerId, gameId, actionDto.UnitId);
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning("Game Logic Error for Player {PlayerId} in Game {GameId} evolving unit {UnitId}: {Message}",
                    playerId, gameId, actionDto.UnitId, ex.Message);
                await Clients.Caller.ReceiveError($"Evolve Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing Evolve for Player {PlayerId} in Game {GameId}, Unit {UnitId}",
                    playerId, gameId, actionDto.UnitId);
                await Clients.Caller.ReceiveError("An unexpected server error occurred while evolving the unit.");
            }
        }

        /// <summary>
        /// 客户端调用此方法来请求执行“攻击”动作。
        /// </summary>
        /// <param name="actionDto">包含攻击动作所需参数的 DTO (攻击者和目标单位 ID)。</param>
        public async Task PerformAttack(AttackActionDto actionDto)
        {
             var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync();
             if (gameId == Guid.Empty || string.IsNullOrEmpty(playerId)) return;

             try
             {
                 _logger.LogInformation("Player {PlayerId} in Game {GameId} declaring attack: Attacker={AttackerUid}, Target={TargetUid}",
                     playerId, gameId, actionDto.AttackerUid, actionDto.TargetUid);

                 // 调用核心游戏逻辑服务处理攻击动作
                 await _gameService.AttackAsync(gameId.ToString(), actionDto); // 确保 AttackAsync 接收 ActionDto 或重载

                 _logger.LogInformation("Player {PlayerId} successfully initiated attack: Attacker={AttackerUid}, Target={TargetUid} in Game {GameId}",
                     playerId, gameId, actionDto.AttackerUid, actionDto.TargetUid);
             }
             catch (GameLogicException ex)
             {
                 _logger.LogWarning("Game Logic Error for Player {PlayerId} in Game {GameId} attacking (Attacker={AttackerUid}, Target={TargetUid}): {Message}",
                     playerId, gameId, actionDto.AttackerUid, actionDto.TargetUid, ex.Message);
                 await Clients.Caller.ReceiveError($"Attack Error: {ex.Message}");
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Unexpected error processing Attack for Player {PlayerId} in Game {GameId} (Attacker={AttackerUid}, Target={TargetUid})",
                     playerId, gameId, actionDto.AttackerUid, actionDto.TargetUid);
                 await Clients.Caller.ReceiveError("An unexpected server error occurred during the attack.");
             }
        }

        // ... 其他动作方法，如 ... (可以保留或移除这行注释)

        // --- 连接管理 ---

        /// <summary>
        /// 当一个新客户端连接到 Hub 时调用。
        /// 主要职责是：
        /// 1. 从连接上下文中（如 QueryString 或 HttpContext）识别游戏 ID 和玩家 ID。
        /// 2. 将连接添加到对应的游戏 SignalR Group 中。
        /// 3. (可选) 向新连接的客户端推送当前游戏状态。
        /// 4. 记录日志。
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            // 1. 识别 Game ID 和 Player ID (简化示例：从 QueryString 读取)
            // 在实际应用中，认证和授权应该在此之前完成，可以通过 Context.User 获取用户信息
            var httpContext = Context.GetHttpContext();
            var gameIdStr = httpContext?.Request.Query["gameId"].ToString();
            var playerId = httpContext?.Request.Query["playerId"].ToString(); // 或者从 Context.User.Identity 获取

            if (Guid.TryParse(gameIdStr, out Guid gameId) && !string.IsNullOrEmpty(playerId))
            {
                 // 2. 添加到 Group
                 string groupName = $"Game_{gameId}";
                 await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

                 _logger.LogInformation("Client connected: ConnectionId={ConnectionId}, PlayerId={PlayerId}, GameId={GameId}, Added to Group={GroupName}",
                     Context.ConnectionId, playerId, gameId, groupName);

                 // 3. 主动推送当前游戏状态给新连接者
                 // GameService.GetOrCreateGameStateAsync 负责生成或获取状态。
                 // 推送操作可能由 GameService 内部通过 IHubContext 完成，或由 Hub 在此调用。
                 // 此处演示 Hub 直接推送的概念流程：
                 try
                 {
                     // 假设 GameService 返回了状态或需要 Hub 推送
                     // 注意：实际实现中 GetOrCreateGameStateAsync 可能已包含推送逻辑
                     GameStateDto currentState = await _gameService.GetOrCreateGameStateAsync(gameId.ToString());
                     await Clients.Caller.ReceiveGameState(currentState);
                     _logger.LogInformation("Sent initial game state to Player {PlayerId} in Game {GameId}", playerId, gameId);
                 }
                 catch (Exception ex)
                 {
                     _logger.LogError(ex, "Error getting or sending initial game state for Player {PlayerId} in Game {GameId}", playerId, gameId);
                     await Clients.Caller.ReceiveError("Failed to load initial game state.");
                 }

                 // TODO M6+: 需要更健壮的连接管理，处理同一玩家多设备连接、断线重连等场景。
                 // 可能需要一个 ConnectionMappingService 来管理 PlayerId <-> ConnectionId 的关系。
            }
            else
            {
                _logger.LogWarning("Client connected with invalid parameters: ConnectionId={ConnectionId}, gameId='{GameIdStr}', playerId='{PlayerId}'. Connection aborted.",
                    Context.ConnectionId, gameIdStr, playerId);
                // 如果无法识别，可以强制断开连接
                Context.Abort();
            }

            await base.OnConnectedAsync();
        }


        /// <summary>
        /// 当一个客户端断开连接时调用。
        /// 主要职责是：
        /// 1. 从 Group 中移除连接 (SignalR 通常会自动处理，但显式调用更清晰)。
        /// 2. 记录日志。
        /// 3. (可选) 通知游戏服务玩家断线，可能需要更新游戏状态或通知对手。
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
             // 尝试从连接上下文中再次获取 Game ID 和 Player ID，如果 OnConnectedAsync 存储了它们会更容易
             // 简化处理：我们仅记录 ConnectionId
             _logger.LogInformation("Client disconnected: ConnectionId={ConnectionId}. Reason: {ExceptionMessage}",
                 Context.ConnectionId, exception?.Message ?? "Normal closure");

             // 1. 从 Group 中移除 (虽然会自动处理，但可以显式做)
             // 需要知道它属于哪个 Group，这通常需要在 OnConnectedAsync 时存储映射关系
             var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync(); // 尝试获取信息
             if (gameId != Guid.Empty) {
                 string groupName = $"Game_{gameId}";
                 await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
                  _logger.LogInformation("Removed ConnectionId {ConnectionId} from Group {GroupName}", Context.ConnectionId, groupName);
             }

             // 3. (可选) 通知游戏服务玩家断线
             // if (gameId != Guid.Empty && !string.IsNullOrEmpty(playerId)) {
             //    await _gameService.HandlePlayerDisconnectAsync(gameId.ToString(), playerId, Context.ConnectionId);
             // }

             // TODO M6+: 实现更可靠的断线/重连逻辑。

             await base.OnDisconnectedAsync(exception);
        }

        // --- 辅助方法 ---
        /// <summary>
        /// (Helper Method - Placeholder)
        /// 从当前 Hub 连接的上下文中提取游戏 ID 和玩家 ID。
        /// **注意:** 这是一个简化实现，依赖于 QueryString。
        /// 生产环境应结合认证信息 (Context.User) 和/或连接映射服务。
        /// </summary>
        /// <returns>包含 GameId 和 PlayerId 的元组。如果无法获取，返回 (Guid.Empty, null)。</returns>
        private Task<(Guid GameId, string? PlayerId)> GetGamePlayerInfoFromContextAsync()
        {
            var httpContext = Context.GetHttpContext();
            var gameIdStr = httpContext?.Request.Query["gameId"].ToString();
            var playerId = httpContext?.Request.Query["playerId"].ToString(); // Or Context.User.Identity.Name

            if (Guid.TryParse(gameIdStr, out Guid gameId) && !string.IsNullOrEmpty(playerId))
            {
                return Task.FromResult((gameId, playerId));
            }

            _logger.LogWarning("Could not extract GameId/PlayerId from Hub Context for ConnectionId {ConnectionId}", Context.ConnectionId);
            return Task.FromResult((Guid.Empty, (string?)null));
        }
    }
}
```

### 2.3 数据传输对象 (Data Transfer Objects - DTOs)

定义用于在客户端和服务器之间传输数据的 C# 类。

#### 2.3.0 新增/核心 DTO 字段与枚举 (New/Core DTO Fields &amp; Enums)

为了支持核心游戏流程，特别是回合管理和状态表示，新增或修改了以下关键 DTO 字段和枚举：

##### 枚举定义 (Enum Definitions)

```csharp
// 可能放在 Dtos/Enums 目录下

// 游戏当前阶段 (用于状态同步和控制)
public enum GamePhase
{
    WaitingForPlayers, // 等待玩家连接
    Initialization,   // 游戏初始化 (洗牌、起始手牌)
    Mulligan,         // 起手换牌阶段
    TurnStart,        // 回合开始阶段 (资源、抽卡)
    MainPhase,        // 主要行动阶段 (出牌、攻击、进化)
    TurnEnd,          // 回合结束阶段 (触发效果)
    GameOver          // 游戏结束
}

// 游戏整体状态
public enum GameStatus
{
    InProgress,
    Player1Win,
    Player2Win,
    Draw // 平局 (例如双方同时在回合结束效果中死亡)
}
```

##### 核心 DTO 更新说明

- **`GameStateDto`:**

### 3.2 核心游戏流程实现 (Core Game Flow Implementation)

本节重点描述 `GameService` 中处理核心游戏流程的方法，特别是回合结束和开始的逻辑。

#### 3.2.1 `GetOrCreateGameStateAsync(string gameId)` (已在 `GameHub` 中初步使用)

- **职责:** 根据 `gameId` 获取或创建新的游戏状态。
- **逻辑:**
  1.  尝试从状态存储（目前是 `ConcurrentDictionary`) 中获取 `gameId` 对应的 `GameStateDto`。
  2.  如果找到，直接返回。
  3.  如果找不到，创建新的 `GameStateDto` 实例：
      - 生成两个 `PlayerStateDto` (Player1, Player2)。
      - 分配 `PlayerId`。
      - **随机决定先手:** `FirstPlayerId` 随机设为 Player1 或 Player2。
      - 设置双方初始状态 (HP, MaxPP=0, MaxEP=根据先后手规则, DeckCount 等)。
      - 洗牌 (需加载卡组定义)。
      - 抽取初始手牌。
      - 设置 `CurrentPlayerId` 为 `FirstPlayerId`。
      - 设置 `CurrentPhase = GamePhase.Mulligan` (或 `Initialization` 如果跳过换牌)。
      - 设置 `GameStatus = GameStatus.InProgress`。
      - 将新创建的 `GameStateDto` 添加到状态存储中。
  4.  返回创建或获取的 `GameStateDto`。

#### 3.2.2 `EndTurnAsync(string gameId, string requestingPlayerId)` (由 `GameHub.EndTurn` 调用)

- **职责:** 处理玩家结束回合的请求。
- **逻辑:**
  1.  获取 `gameId` 对应的 `GameStateDto`。
  2.  **验证:**
      - 检查 `requestingPlayerId` 是否为 `CurrentPlayerId`。
      - 检查 `CurrentPhase` 是否为 `MainPhase`。
      - 如果验证失败，抛出 `GameLogicException`。
  3.  **回合结束效果处理:** (M5+ 范围) 触发当前玩家场上/手牌/主战者的“回合结束时”效果。
  4.  设置 `CurrentPhase = GamePhase.TurnEnd`，并广播事件 (可选)。
  5.  **切换玩家:** 将 `CurrentPlayerId` 切换为对手 ID。
  6.  **启动新回合:** 调用内部方法 `StartTurnInternal(gameState)` (或直接在此处理)。
  7.  **广播事件:** （在 `StartTurnInternal` 处理完毕后或过程中）广播 `TurnEndEvent` 和后续 `TurnStartEvent` 等。

#### 3.2.3 `StartTurnInternal(GameStateDto gameState)` (由 `EndTurnAsync` 或游戏初始化调用)

- **职责:** 处理新回合开始的流程。
- **逻辑:** (严格按照 `完整游戏流程图.md` 的顺序)
  1.  **设置阶段:** `gameState.CurrentPhase = GamePhase.TurnStart`。
  2.  **回合数递增:** `gameState.TurnNumber++`。
  3.  **PP 上限增加 & 回满:**
      - 获取当前玩家 (`CurrentPlayerId`) 的 `PlayerStateDto`。
      - 如果 `player.MaxPp < 10`，则 `player.MaxPp++`。
      - `player.CurrentPp = player.MaxPp`。
      - 广播 `PpUpdateEvent` (包含新 CurrentPp 和 MaxPp)。
  4.  **EP 点数增加:** (如果适用，如第 5 回合开始时增加)
      - 根据先后手和回合数判断是否增加 `MaxEp`。
      - 如果增加，更新 `player.MaxEp`。
      - 恢复 `player.CurrentEp = player.MaxEp` (如果规则是每回合回满 EP 的话，或者仅在特定回合回满)。
      - 广播 `EpUpdateEvent` (如果 EP 变动)。
  5.  **重置单位状态:**
      - 遍历当前玩家场上所有 `UnitStateDto` (`player.Field`)。
      - 设置 `unit.CanAttack = true` (如果单位没有“无法攻击”效果)。
      - 重置 `unit.AttacksThisTurn = 0`。
      - 广播 `UnitStateUpdateEvent` (包含所有状态重置的单位)。
  6.  **回合开始效果:** (M5+ 范围) 触发当前玩家场上/手牌/主战者的“回合开始时”效果。
  7.  **抽卡:**
      - 检查牌库 `player.DeckCount > 0`。
      - 如果 > 0:
        - `player.DeckCount--`。
        - 从牌库抽取一张卡牌 (需要卡牌逻辑)。
        - **检查手牌上限:** 如果 `player.Hand.Count >= 9`，弃掉抽取的卡牌 (触发“爆牌”效果，M5+)。
        - 否则，将卡牌加入 `player.Hand`。
        - 广播 `CardDrawnEvent` **仅给当前玩家** (包含卡牌信息)。
        - 广播 `OpponentCardDrawnEvent` **给对手** (不含卡牌信息，仅通知抽卡动作)。
      - 如果 == 0 (牌库抽空):
        - 设置 `gameState.GameStatus` 为对手胜利 (`Player1Win` 或 `Player2Win`)。
        - 设置 `gameState.WinnerPlayerId`。
        - 设置 `gameState.CurrentPhase = GamePhase.GameOver`。
        - 广播 `GameOverEvent`。
        - **流程终止。**
  8.  **切换到主要阶段:** 如果游戏未结束，设置 `gameState.CurrentPhase = GamePhase.MainPhase`。
  9.  **广播最终状态:** （可选）广播一次包含主要变化的 `GameStateUpdateEvent`，或依赖之前的细分事件。

```mermaid
graph TD
    A[Start TurnInternal] --> B{Set Phase: TurnStart};
    B --> C{Increment TurnNumber};
    C --> D{Increase & Refill PP};
    D --> E{Add & Refill EP?};
    E --> F{Reset Unit States (CanAttack=true)};
    F --> G{Trigger 'Start of Turn' Effects};
    G --> H{Deck > 0?};
    H -- Yes --> I{Decrement DeckCount};
    I --> J{Draw Card};
    J --> K{Hand < 9?};
    K -- Yes --> L{Add Card to Hand};
    K -- No --> M{Discard Card (Burn)};
    L --> N{Broadcast Draw Events};
    M --> N;
    H -- No --> O{Game Over: Deck Out};
    N --> P{Set Phase: MainPhase};
    O --> Q[End Flow];
    P --> Q;

    subgraph Broadcast Events
        D --> EV_PP(PpUpdateEvent);
        E --> EV_EP(EpUpdateEvent);
        F --> EV_Unit(UnitStateUpdateEvent);
        N --> EV_Draw(CardDrawn/OpponentCardDrawn);
        O --> EV_Over(GameOverEvent);
    end

```

#### 3.2.4 事件广播封装 `BroadcastGameEventAsync`

- `GameService` 中应有一个私有辅助方法来处理向 `gameId` 对应的 Group 广播事件，包含日志记录。
- 单点事件 (如错误、抽

  - `CurrentPhase (GamePhase)`: 表示游戏当前的阶段。
  - `GameStatus (GameStatus)`: 表示游戏是否结束及结果。
  - `TurnNumber (int)`: 当前游戏的回合数。
  - `CurrentPlayerId (string)`: 当前回合玩家的 ID。
  - `FirstPlayerId (string)`: 先手玩家的 ID (用于 PP 计算)。
  - `IsPlayer1First (bool)`: (可选，冗余) 方便判断是否先手。
  - `WinnerPlayerId (string?)`: 游戏结束时的胜利者 ID (如果 GameStatus 不是 InProgress)。

- **`PlayerStateDto`:**

  - `PlayerId (string)`: 玩家唯一标识。
  - `CurrentHp (int)`: 当前生命值。
  - `MaxHp (int)`: 最大生命值 (通常 20)。
  - `CurrentPp (int)`: 当前可用 Play Points。
  - `MaxPp (int)`: 当前回合最大 Play Points 上限。
  - `CurrentEp (int)`: 当前可用 Evolve Points。
  - `MaxEp (int)`: 最大 Evolve Points 上限 (通常先手 2, 后手 3)。
  - `IsFirst (bool)`: 是否为先手玩家。
  - `Hand (List<CardDto>)`: 手牌列表。
  - `DeckCount (int)`: 牌库剩余卡牌数量。
  - `Field (List<UnitStateDto>)`: 场上单位列表。 (注意：服务器端可能需要 List<UnitState?> 或类似结构处理空位)。
  - `Graveyard (List<CardDto>)`: 墓地卡牌列表 (可选，根据需要)。
  - `MulliganState (string)`: (临时) 换牌阶段的状态 ("Waiting", "Done")。
  - `IsCurrentTurn (bool)`: (冗余) 是否为该玩家的当前回合。

- **`UnitStateDto`:**
  - `Uid (Guid)`: 单位在场上的唯一实例 ID。
  - `CardId (string)`: 单位对应的卡牌定义 ID。
  - `CurrentAttack (int)`: 当前攻击力。
  - `CurrentDefense (int)`: 当前生命值。
  - `MaxDefense (int)`: 最大生命值。
  - `CanAttack (bool)`: 本回合是否还能攻击。
  - `IsEvolved (bool)`: 是否已进化。
  - `Effects (List<string>)`: (简化

#### 2.3.1 Action DTOs (客户端 -> 服务器)

这些 DTO 用于封装客户端发起的动作的参数。

````csharp
// File: ShadowverseBeyond.Server.Dtos.SignalR.PlayCardActionDto.cs (Illustrative path)
using System;
using System.Collections.Generic; // Required for List<>

namespace ShadowverseBeyond.Server.Dtos.SignalR
{
    /// <summary>
    /// DTO for the client action of playing a card.
    /// </summary>
    public class PlayCardActionDto
    {
        /// <summary>
        /// Unique ID of the card instance being played from the player's hand.
        /// </summary>
        public Guid CardUidInHand { get; set; }

        /// <summary>
        /// Optional list of unique IDs for units on the field targeted by the card's effect.
        /// Null or empty if no targets or target is the opponent leader.
        /// </summary>
        public List<Guid>? TargetUnitIds { get; set; }

        /// <summary>
        /// Optional 0-based index on the player's side of the field where the unit will be played.
        /// Required for playing follower cards. Null otherwise.
        /// </summary>
        public int? FieldIndex { get; set; }

        // TODO: Add Evolve
/// <summary>
    /// DTO for the client action of evolving a unit.
    /// </summary>
    public class EvolveActionDto
    {
        /// <summary>
        /// Unique ID of the unit instance on the field to be evolved.
        /// </summary>
        public Guid UnitId { get; set; }

        // Note: PlayerId is implicitly obtained from the Hub context.
    }

    /// <summary>
    /// DTO for the client action of declaring an attack.
    /// </summary>
    public class AttackActionDto
    {
        /// <summary>
        /// Unique ID of the attacking unit instance on the field.
        /// </summary>
        public Guid AttackerUid { get; set; }

        /// <summary>
        /// Unique ID of the target unit instance on the field, or a predefined Guid/null for the opponent leader.
        /// </summary>
        public Guid TargetUid { get; set; }
    }

    // TODO: Add DTOs for target selection response, etc. if needed
## 3. 后端实现细节 (Backend Implementation Details)

### 3.1 GameService 与 SignalR Hub 的交互

`GameService` 是处理核心游戏逻辑的地方。当游戏状态发生变化或需要与客户端交互时，`GameService` 需要通过 SignalR Hub 将信息发送出去。

- **依赖注入:** `GameService` 需要注入 `IHubContext<GameHub, IGameClient>`。这个上下文允许 `GameService` 在 Hub 之外调用客户端的方法。

  ```csharp
  // In GameService.cs
  private readonly IHubContext<GameHub, IGameClient> _hubContext;
  private readonly ILogger<GameService> _logger; // Add logger

  public GameService(IHubContext<GameHub, IGameClient> hubContext, ILogger<GameService> logger /*, other dependencies */)
  {
      _hubContext = hubContext;
      _logger = logger;
      // ...
  }
````

- **发送事件/状态:** 当游戏逻辑处理完毕后，`GameService` 使用 `_hubContext` 将结果推送给相关的客户端。

  ```csharp
  // Example within GameService method (e.g., after resolving an attack)
  private async Task BroadcastGameEventAsync(string gameId, GameEventDto gameEvent)
  {
      _logger.LogInformation($"Broadcasting event {gameEvent.EventType} to game {gameId}");
      await _hubContext.Clients.Group(gameId).ReceiveGameEvent(gameEvent);
  }

  private async Task SendErrorToPlayerAsync(string gameId, string playerId, string errorMessage)
  {
      // Need a way to map playerId to connectionId(s) if not using User(...)
      // This mapping logic needs careful design (See Connection Management)
      // For now, assuming we can get the connection ID(s) for the player in the specific game
      var connectionIds = GetConnectionIdsForPlayer(gameId, playerId); // Placeholder function
      if (connectionIds != null &amp;&amp; connectionIds.Any()) // Check if list is not null and not empty
      {
           _logger.LogWarning($"Sending error to player {playerId} in game {gameId}: {errorMessage}");
          foreach (var connectionId in connectionIds) {
              await _hubContext.Clients.Client(connectionId).ReceiveError(errorMessage);
          }
      } else {
          _logger.LogError($"Could not find connection IDs for player {playerId} in game {gameId} to send error.");
      }
  }

  // Example: Handling target selection request
  public async Task RequestTargetSelectionFromPlayer(string gameId, string playerId, TargetSelectionRequestDto request)
  {
      var connectionIds = GetConnectionIdsForPlayer(gameId, playerId); // Placeholder
      if (connectionIds != null &amp;&amp; connectionIds.Any()) // Check if list is not null and not empty
      {
          _logger.LogInformation($"Requesting target selection (ID: {request.RequestId}) from player {playerId} in game {gameId}");
           // We might only send to one connection if multiple are not supported/intended
          await _hubContext.Clients.Client(connectionIds.First()).RequestTargetSelection(request);
          // TODO: Store the pending request state (e.g., associate RequestId with the ongoing action)
      } else {
           _logger.LogError($"Could not find connection IDs for player {playerId} in game {gameId} to request target selection.");
           // Handle error: Maybe cancel the action?
      }
  }

  // Placeholder - connection mapping needs proper implementation
  private List<string>? GetConnectionIdsForPlayer(string gameId, string playerId) {
      // TODO: Implement actual mapping logic. This is crucial.
      // Needs to retrieve connection IDs associated with a specific player within a specific game.
      // Options:
      // 1. Maintain a mapping in GameService or a dedicated connection manager service.
      // 2. Query Hub Context somehow (less likely/reliable outside Hub).
      // 3. Use SignalR User IDs if integrated with authentication.
      _logger.LogWarning($"Placeholder GetConnectionIdsForPlayer called for Game {gameId}, Player {playerId}. Returning null.");
      return null; // Placeholder
  }

  ```

- **处理 Hub 调用:** `GameHub` 中的动作方法（如 `

PerformPlayCard`) 直接调用 `GameService`中对应的业务逻辑方法（如`PlayCardAsync`）。`GameService`处理完逻辑后，再通过`\_hubContext` 发送事件。

### 3.3 连接管理与分组 (Connection Management & Grouping)

确保消息只发送给正确的游戏参与者至关重要。

- **Game ID:** 必须有一种方式将每个 SignalR 连接与一个特定的 `gameId` 关联起来。
- **Player ID:** 同样需要将连接与特定的 `playerId` 关联。
- **认证与授权:** （M5 范围可能不包含，但需考虑）理想情况下，`playerId` 应通过身份验证机制获得，而不是仅依赖客户端查询参数。
- **分组:** 使用 SignalR Groups 是最直接的方式。
  - 在玩家成功连接并加入游戏后 (`OnConnectedAsync` 或特定 `JoinGame` Hub 方法)，将其连接 ID 加入到以 `gameId` 命名的 Group 中。
  - `await Groups.AddToGroupAsync(Context.ConnectionId, gameId);`
  - 在玩家断开连接时 (`OnDisconnectedAsync`)，将其从 Group 中移除。
  - `await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId);`
  - `GameService` 通过 `_hubContext.Clients.Group(gameId)` 向特定游戏的所有连接发送消息。
- **连接映射:** **(挑战)** `GameService` 需要知道如何将 `playerId` 映射到具体的 `connectionId(s)`，以便发送单点消息（如 `ReceiveError`, `RequestTargetSelection`）或处理特定玩家的响应。
  - **方案 A (状态维护):** `GameService` 或一个专门的 `ConnectionManagerService` 维护一个映射： `Dictionary<string (gameId), Dictionary<string (playerId), List<string (connectionId)>>>`。这个映射在 `OnConnectedAsync` 和 `OnDisconnectedAsync` 时更新。需要处理并发和状态同步。
  - **方案 B (SignalR 用户):** 如果集成了 ASP.NET Core Identity 或类似身份验证，可以为每个用户分配唯一的 User ID（通常就是 `playerId`），然后可以使用 `_hubContext.Clients.User(playerId)` 发送消息。这要求 Hub 能正确识别用户身份 (`Context.UserIdentifier`)。这是更推荐的方式，但可能超出 M5 初期范围。
  - **当前选择 (待定):** M5 初期可能需要依赖方案 A 的简化版（例如在 `GameService` 内维护一个简单的内存映射），并在后续迭代中转向方案 B。**必须在 Hub 的 `OnConnectedAsync` 和 `OnDisconnectedAsync` 中实现可靠的映射更新逻辑。**

---

**[实施状态更新 - 2025/05/25]**

- **后端基础结构:** **已完成**。

  - `GameHub`, `IGameClient`, `IGameService` (包含基本状态生成逻辑), 相关 DTOs (`GameStateDto`, `PlayerStateDto`, `CardDto` 等), `GameLogicException` 已按设计实现。
  - `Program.cs` 已配置依赖注入和 SignalR 路由。
  - 后端项目 (`Server/ShadowverseBeyond.Server.csproj`) 可成功编译运行。

- **前端 SignalR 集成:** **已完成并调试通过**。

  - 客户端 (`src/main.js`) 成功使用 `@microsoft/signalr` npm 包建立与 `/gamehub` 的连接，并处理连接生命周期事件。
  - 客户端成功注册 `ReceiveGameState` 事件处理器，并在连接后接收服务器推送的初始完整状态。
  - `gameManager.updateFullGameState()` 函数成功实现并用于处理接收到的 `GameStateDto`，更新本地 `gameState` 并触发 `ui.renderGame()`。
  - 客户端 (`src/gameManager.js`) 中的动作请求函数 (如 `playCardRequest`, `endTurnRequest`) 已重构为使用 `connection.invoke()` 向 `GameHub` 发送请求。
  - 已解决与 SignalR 集成相关的客户端 Bug (如对手 ID 错误, 卡牌 `?/?` 显示, `getPlayerState` 错误)。
  - 前端不再依赖 `fetch` API 进行核心游戏交互。

- **当前状态:**
  - 实现了基于 SignalR 的完整状态同步（连接 -> 服务器推送 `ReceiveGameState` -> 客户端处理并渲染）。
  - 实现了客户端通过 SignalR `invoke` 发起动作请求的机制。
  - **待办:** 服务器端需完整实现对 `invoke` 动作请求的处理逻辑 (`GameService`)；设计并实现增量事件推送 (`ReceiveGameEvent`) 及客户端处理。

---

### 3.4 关键交互流程 (Key Interaction Flows) - Mermaid Sequence Diagrams

#### 3.4.1 玩家出牌 (无交互)

````mermaid
sequenceDiagram
    participant Client
    participant GameHub
#### 3.4.4 单位进化 (Evolve Unit)

```mermaid
    sequenceDiagram
        participant Client
        participant GameHub
        participant GameService
        participant OtherClients

        Client->>+GameHub: PerformEvolve(actionDto)
        GameHub->>+GameService: EvolveAsync(gameId, actionDto)
        Note over GameService: 处理进化逻辑 (扣 EP, 检查条件, 获取进化后卡牌, 替换场上单位, 触发进化时效果)
        GameService-->>-GameHub: (Task completes) // EvolveAsync returns Task, events broadcast via HubContext

        alt 进化成功并产生事件
            GameService->>GameHub (via HubContext): BroadcastGameEventAsync(gameId, EpUpdateEventDto(...))
            GameHub->>Client: ReceiveGameEvent(EpUpdateEventDto)
            GameHub->>OtherClients: ReceiveGameEvent(EpUpdateEventDto)

            GameService->>GameHub (via HubContext): BroadcastGameEventAsync(gameId, StatChangeEventDto(...)) // If stats change
            GameHub->>Client: ReceiveGameEvent(StatChangeEventDto)
            GameHub->>OtherClients: ReceiveGameEvent(StatChangeEventDto)

            GameService->>GameHub (via HubContext): BroadcastGameEventAsync(gameId, UnitEvolvedEventDto(...))
            GameHub->>Client: ReceiveGameEvent(UnitEvolvedEventDto)
            GameHub->>OtherClients: ReceiveGameEvent(UnitEvolvedEventDto)
            Note over Client, OtherClients: 接收事件, 更新本地状态/UI
        end
        alt 进化失败 (GameLogicException)
            GameService-->>GameHub: Throws GameLogicException
            GameHub->>-Client: ReceiveError(errorMessage)
        end
        alt 意外错误
             GameService-->>GameHub: Throws Exception
             Note over GameHub: Log Error
             GameHub->>-Client: ReceiveError("Unexpected error...")
        end

````

    participant GameService
    participant OtherClients

    Client->>+GameHub: PerformPlayCard(actionDto)
    GameHub->>+GameService: PlayCardAsync(gameId, playerId, actionDto)
    Note over GameService: 处理出牌逻辑 (扣费, 卡牌效果, 状态变更)
    GameService-->>-GameHub: (Task completes)
    alt 游戏状态改变
        GameService->>GameHub (via HubContext): BroadcastGameEventAsync(gameId, eventDto1)
        GameHub->>Client: ReceiveGameEvent(eventDto1)
        GameHub->>OtherClients: ReceiveGameEvent(eventDto1)
        GameService->>GameHub (via HubContext): BroadcastGameEventAsync(gameId, eventDto2)
        GameHub->>Client: ReceiveGameEvent(eventDto2)
        GameHub->>OtherClients: ReceiveGameEvent(eventDto2)
        Note over Client, OtherClients: 接收事件, 更新本地状态/UI
    end
    alt 出牌失败 (GameLogicException)
        GameService-->>GameHub: Throws GameLogicException
        GameHub->>-Client: ReceiveError(errorMessage)
    end
    alt 意外错误
         GameService-->>GameHub: Throws Exception
         Note over GameHub: Log Error
         GameHub->>-Client: ReceiveError("Unexpected error...")
    end

````

#### 3.4.2 玩家出牌 (需要目标选择)

```mermaid
sequenceDiagram
    participant Client
    participant GameHub
    participant GameService
    participant OtherClients

    Client->>+GameHub: PerformPlayCard(actionDto)
    GameHub->>+GameService: PlayCardAsync(gameId, playerId, actionDto)
    Note over GameService: 处理出牌逻辑, 检测到需要目标选择
    GameService->>GameHub (via HubContext): RequestTargetSelectionFromPlayer(gameId, playerId, requestDto)
    Note over GameService: 暂存当前操作状态 (等待响应)
    GameHub->>Client: RequestTargetSelection(requestDto)
    GameService-->>-GameHub: (PlayCardAsync Task might await or return early)

    Note over Client: UI 显示目标选择界面
    Client->>+GameHub: RespondToTargetSelection(responseDto)
    GameHub->>+GameService: ProcessTargetSelectionResponseAsync(gameId, playerId, responseDto)
    Note over GameService: 处理选择结果, 继续之前的出牌逻辑
    GameService-->>-GameHub: (Task completes)

    alt 游戏状态改变 (选择后)
        GameService->>GameHub (via HubContext): BroadcastGameEventAsync(gameId, eventDto1)
        GameHub->>Client: ReceiveGameEvent(eventDto1)
        GameHub->>OtherClients: ReceiveGameEvent(eventDto1)
        Note over Client, OtherClients: 接收事件, 更新本地状态/UI
    end
    alt 目标选择/处理失败
        Note over GameService: 检测到错误 (e.g., 无效选择)
        GameService->>GameHub (via HubContext): SendErrorToPlayerAsync(gameId, playerId, errorMsg)
        GameHub->>Client: ReceiveError(errorMsg)
    end

````

#### 3.4.3 回合结束与开始

```mermaid
sequenceDiagram
    participant ClientA (Current Turn)
    participant ClientB (Next Turn)
    participant GameHub
    participant GameService

    ClientA->>+GameHub: EndTurn()
    GameHub->>+GameService: EndTurnAsync(gameId, playerAId)
    Note over GameService: 处理回合结束逻辑 (触发效果, 切换玩家) (详见 3.2.2 EndTurnAsync)
    Note over GameService: 处理新回合开始逻辑 (详见 3.2.3 StartTurnInternal, 包括抽卡, PP恢复, 触发效果)

    GameService->>GameHub (via HubContext): BroadcastGameEventAsync(gameId, TurnEndEvent(playerAId))
    GameHub->>ClientA: ReceiveGameEvent(TurnEndEvent)
    GameHub->>ClientB: ReceiveGameEvent(TurnEndEvent)

    GameService->>GameHub (via HubContext): BroadcastGameEventAsync(gameId, TurnStartEvent(playerBId))
    GameHub->>ClientA: ReceiveGameEvent(TurnStartEvent)
    GameHub->>ClientB: ReceiveGameEvent(TurnStartEvent)

    GameService->>GameHub (via HubContext): BroadcastGameEventAsync(gameId, CardDrawnEvent(playerBId, ...))
    GameHub->>ClientB: ReceiveGameEvent(CardDrawnEvent) // Only player B sees the card drawn details
    GameHub->>ClientA: ReceiveGameEvent(OpponentCardDrawnEvent(playerBId)) // Player A sees opponent drew a card

    GameService->>GameHub (via HubContext): BroadcastGameEventAsync(gameId, PpUpdateEvent(...))
    GameHub->>ClientA: ReceiveGameEvent(PpUpdateEvent)
    GameHub->>ClientB: ReceiveGameEvent(PpUpdateEvent)

    GameService-->>-GameHub: (EndTurnAsync Task completes)

```

## 4. 前端实现注意事项 (Frontend Implementation Notes)

前端（最初是网页版，未来可能是 Unity）需要使用 SignalR 客户端库来连接 Hub 并处理通信。

- **连接 Hub:**

  - 使用 `@microsoft/signalr` JavaScript 库（或 Unity 的 C# 客户端库）。

- **构建工具依赖:** 请注意，直接在浏览器中使用 `import * as signalR from "@microsoft/signalr";` 语法需要前端构建工具（如 Vite, Webpack, Rollup 等）的支持来处理模块解析和打包。本项目已根据 [文档/技术文档/M6\_构建工具与开发流程\_V1.0.md](文档/技术文档/M6_构建工具与开发流程_V1.0.md:0) 集成了 Vite 来解决此问题。

  - 建立连接时需要提供 Hub 的 URL (e.g., `/gamehub`)。
  - **关键:** 在连接 URL 中传递必要的参数（如 `gameId`, `playerId` 或认证 token），以便后端 `OnConnectedAsync` 进行识别和分组。例如：

    ```javascript
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/gamehub?gameId=" + gameId + "&playerId=" + playerId) // Add parameters
      .configureLogging(signalR.LogLevel.Information)
      .build();
    ```

    _(注意：直接在 URL 查询字符串中传递 playerId 可能不安全，最好通过认证 Token)_

    **重要初始化流程:** 客户端在成功连接 SignalR 后，通常会执行以下初始化步骤：

  1.  等待服务器通过 `ReceiveGameState` 推送初始游戏状态。
  2.  **调用 `/api/cards` 端点 (见 `API_CardsController_V1.0.md`) 获取卡牌定义数据。**
  3.  使用接收到的游戏状态和卡牌数据完成 UI 的初始渲染。

- **注册服务器方法回调:** 客户端需要注册 `IGameClient` 接口中定义的方法的回调函数。

  ```javascript
  // 连接成功后，服务器会主动推送初始状态
  connection.on("ReceiveGameState", (receivedGameState) => {
    console.log("Received Full GameState from server.");
    // 调用 gameManager 的函数来处理完整的状态更新
    gameManager.updateFullGameState(receivedGameState);
  });

  // 增量游戏事件 (当前服务器尚未实现推送，但客户端处理框架预留)
  connection.on("ReceiveGameEvent", (gameEvent) => {
    console.log("Received GameEvent:", gameEvent.eventType, gameEvent);
    // TODO: 当服务器开始推送增量事件时，完善 gameManager.processGameEvent 逻辑
    // gameManager.processGameEvent(gameEvent);
  });

  // 游戏逻辑错误或警告信息
  connection.on("ReceiveError", (errorMessage) => {
    console.error("Received Error from Server:", errorMessage);
    ui.displayError(errorMessage); // 使用 ui.js 显示错误
  });

  // 服务器请求客户端进行目标选择 (尚未实现)
  connection.on("RequestTargetSelection", (request) => {
    console.log("Received TargetSelectionRequest:", request);
    // ui.showTargetSelection(request); // 使用 ui.js 显示目标选择界面
  });

  // 连接生命周期事件
  connection.onclose((error) => {
    console.error("SignalR Connection Closed.", error);
    ui.displayError(
      "与服务器断开连接。" + (error ? `原因: ${error.message}` : "")
    );
    // 清理连接状态，可能需要禁用部分UI
    if (
      typeof gameManager !== "undefined" &&
      gameManager.setSignalRConnection
    ) {
      gameManager.setSignalRConnection(null); // 通知 gameManager 连接丢失
    }
  });
  ```

- **调用服务器方法:** 客户端通过 `connection.invoke()` 或 `connection.send()` 调用 Hub 上的方法来执行玩家动作。

  // 示例: Player 请求出牌 (注意: 此函数只发送请求, 不修改本地状态)
  async function playCardRequest(cardUid, targets, fieldIndex) {
  if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
  console.error("SignalR connection not available for playCardRequest");
  ui.displayError("连接已断开，无法执行操作。");
  return;
  }
  const actionDto = {
  cardUidInHand: cardUid,
  targetUnitIds: targets,
  fieldIndex: fieldIndex,
  };
  console.log(`[UI->SignalR] Invoking PerformPlayCard:`, actionDto);
  try {
  await connection.invoke("PerformPlayCard", actionDto);
  console.log(`[SignalR->UI] PerformPlayCard invoked successfully.`);
  // 等待服务器通过 ReceiveGameState 或 ReceiveGameEvent 推送状态更新
  } catch (err) {
  console.error(`[SignalR->UI] PerformPlayCard invocation failed:`, err);
  // 错误通常由 ReceiveError 处理，这里仅记录日志
  }
  }

  // 示例: Player 请求结束回合 (注意: 只发送请求)
  async function endTurnRequest() {
  if (!connection || connection.state !== signalR.HubConnectionState.Connected) { /_ ... error handling ... _/ return; }
  console.log(`[UI->SignalR] Invoking EndTurn`);
  try {
  await connection.invoke("EndTurn");
  console.log(`[SignalR->UI] EndTurn invoked successfully.`);
  // 等待服务器推送状态更新
  } catch (err) {
  console.error(`[SignalR->UI] EndTurn invocation failed:`, err);
  }
  }

  // 示例: 响应目标选择 (尚未实现)
  async function submitTargetSelectionResponse(requestId, selectedIds) {
  if (!connection || connection.state !== signalR.HubConnectionState.Connected) { /_ ... error handling ... _/ return; }
  const responseDto = {
  requestId: requestId,
  selectedTargetIds: selectedIds,
  };
  console.log(`[UI->SignalR] Invoking RespondToTargetSelection:`, responseDto);
  try {
  await connection.invoke("RespondToTargetSelection", responseDto);
  console.log(`[SignalR->UI] RespondToTargetSelection invoked successfully.`);
  } catch (err) {
  console.error(`[SignalR->UI] RespondToTargetSelection invocation failed:`, err);
  }
  );
  }

  ```

  ```

- **状态管理:** 前端维护一个本地 `gameState` 对象 (在 `gameManager.js` 中)。当前实现是：服务器连接后推送 `ReceiveGameState`，由 `main.js` 接收并调用 `gameManager.updateFullGameState()` 来**完全替换**本地状态并触发 `ui.renderGame()`。增量事件 (`ReceiveGameEvent`) 的处理逻辑 (`gameManager.processGameEvent`) 已预留，但目前依赖服务器实现推送。
- **错误处理:** 客户端通过注册 `ReceiveError` 处理器来响应服务器推送的游戏逻辑错误，并显示给用户。`invoke` 调用本身的失败主要通过 `catch` 块记录日志，因为具体的业务错误会由 `ReceiveError` 传达。连接错误由 `onclose`, `onreconnecting`, `onreconnected` 处理。
- **交互处理:** 对于 `RequestTargetSelection` (尚未实现)，客户端需要在收到请求后，显示 UI 供用户选择，并将选择结果通过 `invoke("RespondToTargetSelection", ...)` 发回服务器。

## 5. 错误处理与健壮性 (Error Handling & Robustness)

- **Hub 错误处理:**
  - `GameHub` 中的方法应使用 `try-catch` 块捕获调用 `GameService` 时可能抛出的 `GameLogicException`，并通过 `Clients.Caller.ReceiveError()` 将错误信息发送给发起调用的客户端。
  - 捕获预料之外的 `Exception`，记录详细日志（包括 DTO 内容），并向客户端发送通用的错误消息，避免泄露内部细节。
- **GameService 错误处理:**
  - `GameService` 内部应进行详细的业务逻辑验证，并在验证失败时抛出具体的 `GameLogicException`（例如，“PP 不足”，“目标无效”，“非当前回合”）。
  - 对于复杂的交互流程（如目标选择），`GameService` 需要管理状态，并在收到响应时进行验证。如果响应无效或超时，应能妥善处理（例如，取消操作，通知玩家）。
- **连接问题:**
  - 客户端需要处理连接中断 (`onclose`) 事件，并实现适当的重连逻辑（可以使用 SignalR 客户端库内置的重连功能）。
  - 服务器端 (`OnDisconnectedAsync`) 需要处理玩家意外断开的情况，通知 `GameService`，可能需要设置游戏状态为暂停或等待重连。
- **并发与状态:**
  - `GameService` 需要处理并发访问游戏状态的问题（如果 `GameService` 实例是 Singleton 或 Scoped，并且可能处理来自同一游戏的不同连接的并发请求）。使用 `lock` 或其他并发控制机制保护关键代码段。
  - 连接映射 (`playerId` <-> `connectionId`) 的维护需要线程安全。

## 6. 性能与扩展性考虑 (Performance & Scalability Considerations)

- **消息大小:**
  - 尽量减小 DTO 的大小，只包含必要的信息。避免在 `GameEventDto` 中发送冗余或完整的游戏状态。
  - 考虑使用 MessagePack 协议替代 JSON，以获得更好的性能和更小的消息体积（需要前后端都支持和配置）。
- **消息频率:**
  - 避免过于频繁地发送消息。例如，可以将多个连续的细微状态变化合并为一个事件发送。
  - 对于仅影响单个玩家的信息（如抽卡），使用 `Clients.Client(connectionId)` 或 `Clients.User(playerId)` 发送，而不是广播到整个 Group。
- **服务器资源:**
  - SignalR 连接会消耗服务器内存和 CPU 资源。需要监控服务器性能，并根据需要进行扩展。
  - 考虑使用 Azure SignalR Service 或其他托管服务来分担连接管理的负载，实现更好的伸缩性。
- **GameService 扩展性:**
  - `GameService` 的设计应保持模块化，以便未来支持更复杂的游戏逻辑和更多的游戏实例。
  - 如果预期游戏实例数量巨大，可能需要考虑将游戏状态存储从内存转移到外部存储（如 Redis），并重新设计 `GameService` 的状态管理方式。

## 7. 测试策略 (Testing Strategy)

- **单元测试:**
  - `GameService` 中的核心游戏逻辑应尽可能编写单元测试。
  - DTO 本身通常不需要单元测试，但依赖它们的服务需要。
- **集成测试:**
  - 测试 `GameHub` 与 `GameService` 的交互。可以使用 `TestServer` 和内存中的 SignalR 客户端进行测试。
  - 测试 Hub 方法的调用、事件广播和错误处理流程。
- **端到端测试:**
  - 模拟客户端连接 Hub，调用动作，接收并验证事件。可以使用 C# SignalR 客户端库编写测试脚本。
- **手动测试:**
  - 通过实际的前端界面进行游戏测试，验证实时通信的效果和交互流程。
  - **利用管理员后台/调试接口:** 实现 M5 时，应考虑在 `GameService` 或 Hub 中添加内部调试方法（例如，通过特定 Hub 方法或 HTTP API 调用），允许手动触发事件、修改游戏状态（PP、HP、手牌）、模拟特定场景，这将极大方便测试。例如：
    - `Debug_SetPlayerPp(string gameId, string playerId, int pp)`
    - `Debug_DealCardToHand(string gameId, string playerId, string cardId)`
    - `Debug_TriggerGameEvent(string gameId, GameEventDto eventData)`

---

## 8. M5: 前端 SignalR 集成 - 详细实施计划 V1.0 (2025-05-25)

本节详细规划了将前端 Web 应用与已设计的 SignalR 后端 (`GameHub`) 集成的具体步骤和注意事项。

### 8.1 目标 (Goals) - 已完成

本次集成的主要目标**已完成**，包括：

- **已实现:** 将前端核心游戏操作（出牌、结束回合等动作发起）从依赖 `fetch` API 或本地逻辑，完全迁移至通过 SignalR Hub 方法 (`connection.invoke`) 异步发起。
- **已实现:** 前端不再主动轮询或请求游戏状态，改为完全依赖服务器通过 SignalR 推送。
- **已实现:** 实现 SignalR 客户端事件处理机制，能够：
  - 接收并处理服务器推送的完整游戏状态 `ReceiveGameState(GameStateDto gameState)`，通过调用 `gameManager.updateFullGameState()` 更新本地 `gameState` 并驱动 UI 渲染。
  - 接收并处理服务器推送的错误信息 `ReceiveError(string errorMessage)`，并显示给用户。
  - (预留) 为处理增量事件 `ReceiveGameEvent` 和交互请求 `RequestTargetSelection` 预留了处理器框架。
- **已实现:** 前端 JavaScript 代码已使用 `@microsoft/signalr` npm 包，能够正确建立、管理与 `/gamehub` 的连接，并处理连接生命周期事件。
- **已实现:** `index.html` 中对 SignalR CDN 的 `<script>` 引用已被移除。
- **已实现:** 前端初始化时获取 `gameId` 和 `playerId`，并成功用于建立 SignalR 连接。

### 8.2 核心修改点 (Core Modifications)

1.  **`index.html`:**

    - **移除:** 删除 `<script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/...">` 标签。

2.  **项目依赖 (假设使用 npm):**

    - **添加:** 需要执行 `npm install @microsoft/signalr` 并确保构建流程能正确处理 JS 模块。

3.  **`src/main.js` (或新建 `src/signalrService.js` 进行封装):**

    - **导入:** `import * as signalR from "@microsoft/signalr";`
    - **建立连接 (`HubConnection`):**

      ```javascript
      // 在 initGame 或类似入口函数中
      let connection; // 使 connection 在外部可访问，或通过 service 暴露

      async function setupSignalRConnection(gameId, playerId) {
        if (connection) {
          await connection.stop(); // Stop existing connection if any
        }

        const hubUrl = `/gamehub?gameId=${encodeURIComponent(
          gameId
        )}&playerId=${encodeURIComponent(playerId)}`;
        console.log(`Attempting to connect to SignalR Hub: ${hubUrl}`);

        connection = new signalR.HubConnectionBuilder()
          .withUrl(hubUrl) // 传递 gameId 和 playerId
          .withAutomaticReconnect() // 启用自动重连
          .configureLogging(signalR.LogLevel.Information) // 配置日志级别
          .build();

        // 注册事件处理器 (见下方)
        registerSignalREventHandlers(connection);

        try {
          await connection.start();
          console.log(
            "SignalR Connected successfully. Connection ID:",
            connection.connectionId
          );
          // 连接成功后，可能需要请求一次初始游戏状态，或者等待服务器主动推送
          // await connection.invoke("RequestInitialGameState"); // 如果后端需要显式请求
          return connection; // 返回连接实例
        } catch (err) {
          console.error("SignalR Connection Failed: ", err);
          // Display error to user, potentially offer retry mechanism
          ui.displayError("连接游戏服务器失败，请刷新重试。"); // Example UI call
          connection = null; // Reset connection
          throw err; // Re-throw for potential higher-level handling
        }
      }
      ```

      - **注意:** 需要在 `initGame(gameId, playerId, initialGameState)` 中调用 `setupSignalRConnection`。

    - **注册事件处理器 (`registerSignalREventHandlers`):**

      ```javascript
      function registerSignalREventHandlers(conn) {
        // 核心状态更新 (已实现并调试)
        conn.on("ReceiveGameState", (receivedGameState) => {
          console.log("Received Full GameState from server.");
          // 调用 gameManager 的函数来处理完整的状态更新
          gameManager.updateFullGameState(receivedGameState);
        });

        // 增量游戏事件 (当前服务器尚未实现推送，客户端处理框架预留)
        conn.on("ReceiveGameEvent", (gameEvent) => {
          console.log("Received GameEvent:", gameEvent.eventType, gameEvent);
          // TODO: 当服务器开始推送增量事件时，完善 gameManager.processGameEvent 逻辑
          // gameManager.processGameEvent(gameEvent);
        });

        // 游戏逻辑错误或警告信息 (已实现)
        conn.on("ReceiveError", (errorMessage) => {
          console.error("Received Error from Server:", errorMessage);
          ui.displayError(errorMessage); // 使用 ui.js 显示错误
        });

        // 服务器请求客户端进行目标选择 (尚未实现)
        conn.on("RequestTargetSelection", (request) => {
          console.log("Received TargetSelectionRequest:", request);
          // ui.showTargetSelection(request); // 使用 ui.js 显示目标选择界面
        });

        // 连接生命周期事件 (已实现)
        conn.onclose((error) => {
          console.error("SignalR Connection Closed.", error);
          ui.displayError(
            "与服务器断开连接。" + (error ? `原因: ${error.message}` : "")
          );
          // 清理连接状态，可能需要禁用部分UI
          if (
            typeof gameManager !== "undefined" &&
            gameManager.setSignalRConnection
          ) {
            gameManager.setSignalRConnection(null); // 通知 gameManager 连接丢失
          }
        });

        conn.onreconnecting((error) => {
          console.warn("SignalR Connection Reconnecting...", error);
          ui.displayInfo("正在尝试重新连接服务器..."); // Example UI update
        });

        conn.onreconnected((connectionId) => {
          console.log(
            "SignalR Connection Reconnected. New Connection ID:",
            connectionId
          );
          ui.displayInfo("已重新连接到服务器。");
          // 注意：重连后，服务器的 OnConnectedAsync 可能会再次触发并推送 ReceiveGameState
          // 无需客户端在此处显式请求状态
        });
      }
      ```

    - **暴露 Connection:** 需要确保 `gameManager.js` 能访问到 `connection` 实例。可以将其作为参数传递，或者放在一个共享模块/服务中。

4.  **`src/gameManager.js`:**

    - **获取 `connection` 实例:** 修改函数（如 `initGame`）以接收 `connection`，或从 `signalrService` 导入。

      ```javascript
      let signalRConnection; // Module-level variable

      export function setSignalRConnection(conn) {
        signalRConnection = conn;
      }
      ```

    - **重构动作函数:** 将所有发起游戏状态改变的函数修改为调用 `signalRConnection.invoke(...)`。

      ```javascript
      // --- Action Request Functions in gameManager.js (已实现) ---
      // 这些函数现在只负责通过 SignalR 发送请求，不应直接修改本地 gameState。
      // 状态更新完全依赖服务器通过 ReceiveGameState 或 ReceiveGameEvent 推送。

      export async function playCardRequest(cardUid, targets, fieldIndex) {
          if (!signalRConnection || signalRConnection.state !== signalR.HubConnectionState.Connected) {
              console.error("SignalR connection not available for playCardRequest");
              ui.displayError("连接已断开，无法执行操作。");
              return;
          }
          const actionDto = {
              cardUidInHand: cardUid,
              targetUnitIds: targets,
              fieldIndex: fieldIndex,
          };
          console.log(`[GameManager->SignalR] Invoking PerformPlayCard:`, actionDto);
          try {
              await signalRConnection.invoke("PerformPlayCard", actionDto);
              console.log(`[SignalR->GameManager] PerformPlayCard invoked.`);
          } catch (err) {
              console.error(`[SignalR->GameManager] PerformPlayCard invocation failed:`, err);
          }
      }

      // (类似地修改 evolveCard, endTurnRequest 等函数)
      export async function endTurnRequest() {
          if (!signalRConnection || signalRConnection.state !== signalR.HubConnectionState.Connected) { /* ... */ return; }
          console.log(`[GameManager->SignalR] Invoking EndTurn`);
          try {
              await signalRConnection.invoke("EndTurn");
              console.log(`[SignalR->GameManager] EndTurn invoked.`);
          } catch (err) {
              console.error(`[SignalR->GameManager] EndTurn invocation failed:`, err);
          }
      }


      // 攻击流程特殊处理:
      // 1. 用户点击攻击者 -> initiateAttack
      export function initiateAttack(attackerUid) {
           console.log(`[GameManager] Initiating attack selection for attacker: ${attackerUid}`);
           // 更新本地 UI 状态以高亮攻击者和可选目标，不调用服务器
           gameState.uiState = gameState.uiState || {}; //确保uiState存在
           gameState.uiState.selectedAttackerUid = attackerUid;
           ui.renderHighlights(gameState); // 更新高亮显示
      }

      // 2. 用户点击目标 -> selectAttackTarget -> 发起 PerformAttack 请求
      export async function selectAttackTarget(attackerUid, targetUid) {
          if (!signalRConnection || signalRConnection.state !== signalR.HubConnectionState.Connected) { /* ... */ return; }
          console.log(`[GameManager->SignalR] Invoking PerformAttack: Attacker=${attackerUid}, Target=${targetUid}`);
           // 清除本地 UI 状态
           if(gameState.uiState) {
               gameState.uiState.selectedAttackerUid = null;
           }
           ui.renderHighlights(gameState); // 清除高亮 (或者等待服务器事件)

          const dto = { attackerUid: attackerUid, targetUid: targetUid };
           try {
               await signalRConnection.invoke("PerformAttack", dto); // 假设 Hub 方法名为 PerformAttack
               console.log(`[SignalR->GameManager] PerformAttack invoked.`);
           } catch (err) {
                console.error(`[SignalR->GameManager] PerformAttack invocation failed:`, err);
           }
      ```
