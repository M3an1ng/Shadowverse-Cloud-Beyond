# BUG 追踪记录

## 2025-05-31

### BUG-010: AI 先手回合结束后未触发人类玩家回合开始
*   **现象**: AI 先手时，完成其第一个回合的动作（例如打出卡牌）后，游戏卡死。后端日志显示 AI 调用了 `EndTurnAsync`，但后续没有为人类玩家调用 `StartPlayerTurnAsync`。前端日志显示人类玩家收到了回合开始事件，但这似乎是 Mulligan 流程中不合时宜触发的，而非 AI 回合正常结束后的流程。
*   **触发条件**: AI 先手，完成第一个回合后。
*   **状态**: Open
*   **优先级**: Critical (阻碍核心玩法)
*   **初步分析**: 根本原因在于 `AiTurnExecutor.ExecuteTurnAsync` 在调用 `_gameService.EndTurnAsync` 后，没有处理返回结果，特别是没有在检测到下一个玩家是人类时，为其调用 `_gameService.StartPlayerTurnAsync`。
*   **下一步**:
    1.  修改 `GameService.StartPlayerTurnAsync`，使其内部通过 `_hubContext` 广播产生的事件。
    2.  修改 `AiTurnExecutor.ExecuteTurnAsync`，在调用 `_gameService.EndTurnAsync` 后，检查 `endTurnResult.IsNextPlayerAi`。如果为 `false` 且 `endTurnResult.NextPlayerId` 有效，则调用 `await _gameService.StartPlayerTurnAsync(gameId, endTurnResult.NextPlayerId)`。
    3.  进行测试。

### BUG-009: 玩家回合开始逻辑触发两次
*   **现象 (更新 2025-05-31)**: 修复了 AI 先手第一回合开始事件触发两次的问题后，出现了新问题：AI 先手时，其第一个回合结束后，游戏未能进入玩家回合（Turn 2），玩家回合的 PP、手牌更新未发生。
*   **触发条件**: AI 先手，完成第一个回合后。
*   **状态**: Pending Verification (最新重构旨在解决此问题，但测试中发现了新的 BUG-010（AI 回合结束后未开始玩家回合），需要进一步验证此 Bug 是否已完全解决。)
*   **优先级**: Critical (严重影响游戏状态和规则)
*   **初步分析 (更新 2025-05-31 - 最终方案: 分离回合开始逻辑)**: 经过讨论，确定采用更清晰的架构方案。核心问题在于 AI 先手第一回合的特殊性：回合开始逻辑已由 `SubmitMulliganAsync` 调用 `StartPlayerTurnAsync` 完成，后续只需触发 AI 的行动/结束阶段。之前的方案（如添加 `skipStartTurnLogic` 参数）虽然能解决问题，但不如将回合流程彻底分离清晰。**最终方案**：将 `StartPlayerTurnAsync` 作为所有回合（无论玩家类型或是否为第一回合）的标准启动步骤。AI 的行动/结束逻辑将封装在单独的方法中（暂定名为 `ExecuteAiActionPhaseAsync`），不再包含回合开始逻辑。
*   **下一步 (重构计划)**:
    1.  **重构 `GameService.ExecuteAiTurnInternalAsync`**: 移除内部的 `StartPlayerTurnAsync` 调用，并可能重命名为 `ExecuteAiActionPhaseAsync`，使其仅负责调用 `AiTurnExecutor.ExecuteTurnAsync`。
    2.  **重构 `GameService.TriggerAiTurnAsync`**: 调整其逻辑，使其调用新的 `ExecuteAiActionPhaseAsync`。
    3.  **重构 `GameHub.EndTurn`**: 修改逻辑，确保在确定下一玩家后，*总是先*调用 `StartPlayerTurnAsync`，广播事件后，*再判断*如果下一位是 AI，则调用 `TriggerAiTurnAsync`。
    4.  **重构 `GameService.SubmitMulliganAsync`**: 修改逻辑，在调用 `StartPlayerTurnAsync` 并广播事件后，*判断*如果第一位玩家是 AI，则直接调用 `ExecuteAiActionPhaseAsync`。
    5.  更新 [`IGameService.cs`](Server/Services/IGameService.cs) (如果需要)。
    6.  进行测试。
### BUG-007: AI 先手第一回合开始触发两次
*   **现象**: AI 先手时，其回合开始效果（抽卡、PP 增加）被执行了两次。
*   **触发条件**: AI 玩家执先手，Mulligan 结束后。
*   **状态**: Open
*   **优先级**: Critical (严重影响游戏状态)
*   **初步分析**: 怀疑 `GameService.StartPlayerTurnAsync` 或其触发逻辑在 AI 的第一回合被意外调用了两次。可能涉及 `SubmitMulliganAsync`, `ExecuteAiTurnInternalAsync`, `TriggerAiTurnAsync`, `AiTurnExecutor.ExecuteTurnAsync` 之间的调用协调问题。
*   **下一步**: 检查 `GameService` 和 `GameHub` 中与 AI 首回合启动相关的调用链，定位重复触发点。

### BUG-008: AI 出牌未检测 PP 消耗
*   **现象**: AI 可以打出费用超出其当前可用 PP 值的卡牌。
*   **触发条件**: AI 执行出牌动作时。
*   **状态**: Open
*   **优先级**: Critical (破坏游戏规则)
*   **初步分析**: 问题可能在 `AiService` 的决策逻辑（未考虑 PP 限制）或 `GameService.PlayCardAsync` 方法未能正确执行 PP 消耗检查。
*   **下一步**: 待 BUG-007 解决后，检查 `GameService.PlayCardAsync` 中的 PP 检查逻辑和 `AiService` 的出牌决策逻辑。

### BUG-006: AI 先手第一回合无法结束
*   **现象**: 当 AI 先手开始游戏时，AI 会执行出牌等动作，但其回合不会正常结束。观察到前端 UI 已经更新显示为人类玩家的回合状态（例如 PP、手牌），表明后端可能过早地开始了人类玩家的回合，或者前端事件处理顺序有问题，导致 AI 的回合结束流程被“覆盖”或中断。 **(注意: 此问题可能由 BUG-007 引发)**
*   **触发条件**: AI 玩家执先手。
*   **对比**: 当人类玩家先手时，后续 AI 的回合行为（包括结束回合）是正常的。
*   **状态**: Superseded by BUG-010 (此现象的核心问题现在被 BUG-010 更准确地描述和追踪。)
*   **优先级**: Critical (阻碍核心玩法)
*   **初步分析**: 原始分析怀疑是 `SubmitMulliganAsync` 中未使用 `await`。代码检查已确认 `await` 存在。当前怀疑此现象是 BUG-007（回合开始触发两次）导致的副作用。
*   **下一步**: 解决 BUG-010。

## 2025-05-30

### BUG-001: 回合数不更新
*   **现象**: 游戏界面上的回合数始终显示为 Turn 0，即使AI正常结束回合，回合数也不增加。
*   **状态**: 待测试 (Fixed in frontend validation - `src/gameManager.js` `handleTurnStarted`/`handleTurnEnded`)
*   **优先级**: High

### BUG-002: AI 问题
*   **子问题1: AI 不抽卡**
    *   **现象**: AI 在其回合开始时不会抽取卡牌。
    *   **状态**: 已解决 (Fixed backend broadcast for `CardDrawnEventDto` in `Server/Services/GameService.cs`)
    *   **优先级**: Medium
*   **子问题2: AI 攻击报错**
    *   **现象**: AI 尝试攻击时，后端日志出现 `GameLogicException: Attacker unit ... cannot attack this turn`。
    *   **状态**: Open
    *   **优先级**: Medium

### BUG-003: 玩家出牌时报错 "无法获取玩家状态！"
*   **现象**: 玩家在自己的回合尝试从手牌中打出卡牌时，弹出内部错误提示 "无法获取玩家状态！"，导致出牌失败。
*   **状态**: 待测试 (Fixed in frontend validation - `src/gameManager.js` `handleTurnStarted`/`handleTurnEnded`)
*   **优先级**: Critical (阻碍核心玩法)
### BUG-004: AI回合开始时UI冻结/状态覆盖
*   **现象**: 在人机对战中，当人类玩家结束回合后，AI 回合开始时，前端 UI 出现冻结、无法操作或状态被错误覆盖（如 PP/手牌回滚到上一回合）。
*   **根本原因分析 (更新)**: 后端存在**事件广播时序冲突**。由人类玩家结束回合触发的 `GameService.EndTurnAsync` 调用，虽然尝试通过条件判断 (`if (!isNextPlayerAi || currentPlayerState.IsAI)`) 避免直接调用 AI 的 `ProcessStartOfTurn`，但其事件收集和广播机制仍然存在问题。该调用收集的事件（包括 AI 回合开始的相关事件，即使 `ProcessStartOfTurn` 未被显式调用）在其自身流程结束时才广播。此时，由 `AiTurnExecutor` 触发的 AI 回合（包括 AI 的 `EndTurnAsync` 和后续人类玩家的 `StartPlayerTurnAsync`）可能已经完成并广播了正确的事件。这个来自**原始人类 `EndTurnAsync` 调用的延迟广播**，覆盖了前端已经更新的正确状态，导致 UI 冻结或回滚。
*   **修复尝试 (2025-05-30 - 条件判断)**: 在 [`Server/Services/GameService.cs`](Server/Services/GameService.cs:211) 的 `EndTurnAsync` 中添加条件判断 (`if (!isNextPlayerAi || currentPlayerState.IsAI)`) 以阻止为 AI 调用 `ProcessStartOfTurn`。**此尝试失败**，未能解决延迟广播覆盖状态的问题。
*   **状态**: 已解决 (V2 重构完成，待验证 BUG-006)
*   **解决方案 (V2)**: 重构 `EndTurnAsync` 和 `StartPlayerTurnAsync`，分离职责。添加 `TriggerAiTurnAsync` 方法，由 `GameHub` 在检测到下一位是 AI 时调用，该方法 `await` AI 回合执行。修改 `SubmitMulliganAsync`，使其在 AI 先手时 `await` AI 的首回合执行。
*   **优先级**: Critical (严重影响游戏流程和体验)
### BUG-005: 抽卡后前端手牌未更新
*   **现象**: 玩家抽卡后，后端 `HandUpdateEventDto` 中的 `DrawnCards` 列表在前端 `gameManager.js` 中无法正确解析，导致手牌 UI 不显示新抽到的卡牌。
*   **状态**: 已解决
*   **解决日期**: 2025-05-31
*   **解决方案**: 修复了前端 JavaScript ([`src/gameManager.js`](src/gameManager.js:1)) 中处理 `HandUpdateEvent` 时的大小写敏感问题。之前代码尝试访问 `eventData.DrawnCards` (PascalCase)，而实际从后端接收到的属性名是 `eventData.drawnCards` (camelCase)。已将代码修改为使用正确的 `drawnCards`。相关的调试日志也已从前端和后端移除。
*   **优先级**: High (影响核心抽卡反馈)