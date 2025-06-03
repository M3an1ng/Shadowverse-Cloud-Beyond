# Bug 修复计划 V1.0 (已完成)

## 目标

修复 BUG-FE-HYDRATE 和 BUG-BE-TURN，确保：
1.  前端能够正确渲染双方主战者信息。
2.  AI 回合结束后，前端能够正确接收到回合转换事件并更新 UI，允许玩家开始自己的回合。
3.  代码实现与 M5、M7 设计文档保持一致。

## 修复方案

### 1. 修复 BUG-FE-HYDRATE (前端主战者渲染失败)

*   **任务 1.1: 修改 DTO 定义 [已完成]**
    *   **文件:** `Server/Dtos/PlayerStateDto.cs`
    *   **操作:** 添加一个新的属性 `public CardInstanceDto LeaderCard { get; set; }`。
    *   **隐式操作:** 在 `Server/Dtos/CardInstanceDto.cs` 的 `CardZone` 枚举中添加了 `Leader` 值。
    *   **影响:** 需要更新所有使用 `PlayerStateDto` 的地方，确保新字段被正确处理。
*   **任务 1.2: 修改 GameService 状态填充逻辑 [已完成]**
    *   **文件:** `Server/Services/GameService.cs`
    *   **操作:** 在 `GetOrCreateGameStateAsync` 方法中添加了填充 `PlayerStateDto.LeaderCard` 的逻辑。
    *   **实现细节:**
        *   从 `_cardDefinitionRepository` 获取卡牌定义时，**移除了 `.Where(c => c.IsCollectible)` 过滤器**，以确保能加载到非收藏卡牌（如主战者）。
        *   明确使用主战者卡牌定义 ID `0` 来查找并创建 `LeaderCard` 实例。
    *   **影响:** `GameService` 的状态构建逻辑。
*   **任务 1.3: 更新相关文档 (M5) [已完成]**
    *   **文件:** `文档/技术文档/M5_SignalR通信设计_V1.0.md`
    *   **操作:** 更新了 `PlayerStateDto` 定义、`CardZone` 枚举定义，并补充了游戏状态加载逻辑（使用 `[V1.3 注]` 标记）。文档版本未显式提升，通过注释标记变更。
    *   **文件:** (可能) 其他引用 `PlayerStateDto` 的技术文档。
    *   **操作:** (本次未发现其他直接受影响文档)。

### 2. 修复 BUG-BE-TURN (AI 回合结束后前端无响应)

*   **任务 2.1: 恢复并调整 GameService 中的广播逻辑 [已完成]**
    *   **文件:** `Server/Services/GameService.cs`
    *   **操作:** 在 `ExecuteAiActionPhaseAsync` 方法末尾，调用 `StartPlayerTurnAsync` 获取包含完整游戏状态的 `GameStateUpdateEventDto`，并调用 `BroadcastGameEventAsync` 将此事件广播给客户端。
    *   **实现细节:** 确认了广播的事件类型为 `GameStateUpdateEventDto`，符合 M7 V1.1 设计，替代了已弃用的 `TurnStartedEventDto`。
    *   **影响:** `GameService` 的 AI 回合结束处理逻辑。
*   **任务 2.2: 更新相关文档 (M7/M5) [已完成]**
    *   **文件:** `文档/技术文档/M7_核心动作处理与事件广播设计_V1.0.md`
    *   **操作:** 检查发现 M7 V1.1 已准确描述了使用 `GameStateUpdateEventDto` 进行回合开始广播的逻辑，**无需修改**。
    *   **文件:** `文档/技术文档/M5_SignalR通信设计_V1.0.md`
    *   **操作:** M5 已在任务 1.3 中更新，其关于回合转换的描述与 M7 和实现一致。