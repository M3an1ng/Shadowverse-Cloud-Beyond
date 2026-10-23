# 当前活动背景 (Active Context)

## 1. 当前任务/焦点 (Current Task/Focus)

*   **主要目标:** 根据设计文档 [`文档/05-游戏系统/04-01-游戏循环与队列处理设计.md`](文档/05-游戏系统/04-01-游戏循环与队列处理设计.md) 重构游戏事件处理流程。
*   **具体步骤:** 从创建 `PlayerActionWrapper` 类开始，逐步修改 `GameHub` 和 `GameService`。

## 2. 近期决策/变更 (Recent Decisions/Changes)

*   完成了对 `CardQueryExecutor` 的骨架实现和初步编译修复。
*   读取了所有核心 Memory Bank 文件 (`projectbrief.md`, `productContext.md`, `systemPatterns.md`, `techContext.md`, `activeContext.md`, `.clinerules`)。
*   确认将任务优先级切换到事件流程重构。

## 3. 阻塞/风险/问题 (Blockers/Risks/Issues)

*   **Memory Bank MCP 工具路径问题:** `memory_bank_read/write/update` 可能无法正常工作，需要准备使用 `read_file`/`write_to_file` 作为替代方案。
*   **重构复杂度:** 事件流程是核心逻辑，改动范围较大，需要非常谨慎，遵循设计文档和 `.clinerules`。
*   **`Server.Tests` 编译错误:** (优先级降低) 测试项目编译问题将在核心重构稳定后解决。
*   **测试覆盖率不足:** 重构后需要添加或修改大量测试。

## 4. 后续计划 (Next Steps)

1.  **创建 `PlayerActionWrapper` 类:** (当前步骤) 定义用于行动队列的数据结构。
2.  **(后续)** 修改 `GameHub` 方法，移除直接逻辑调用，改为创建 `PlayerActionWrapper` 并调用 `GameService.EnqueuePlayerActionAsync`。
3.  **(后续)** 在 `GameService` 中添加行动队列 (`_playerActionQueues`) 和事件队列 (`_eventQueues`)。
4.  **(后续)** 实现 `GameService.EnqueuePlayerActionAsync` 方法。
5.  **(后续)** 实现 `GameService.GameProcessingLoopAsync` 核心处理循环。
6.  **(后续)** 重构 `GameService` 中的行动方法 (如 `PlayCardAsync`) 为初始步骤执行。
7.  **(后续)** 实现 `UpdateAndBroadcastHandPlayabilityAsync`。
8.  **(后续)** 添加 `ReceiveHandPlayabilityUpdate` 到 `IGameClient`。
9.  **(后续)** 解决 `Server.Tests` 编译问题并补充测试。

*(注意: 此文档会频繁更新以反映当前工作状态)*