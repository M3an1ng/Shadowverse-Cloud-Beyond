# BUG 追踪记录

## 2025-05-31

### BUG-012: 回合计数器错误 (从 0 开始)
*   **现象**: 调度阶段结束后，游戏的第一个回合在 UI 上显示为 "Turn 0"，导致后续所有回合数都比实际少 1。
*   **触发条件**: 游戏开始，调度阶段完成后进入第一个回合时。
*   **状态**: 已解决
*   **解决日期**: 2025-05-31
*   **优先级**: High (影响游戏状态逻辑和显示)
*   **解决方案**: 此问题随 BUG-004 状态水合问题的修复而解决。在统一客户端卡牌标识符 ([`src/cardManager.js`](src/cardManager.js:82)) 并修复 [`src/gameManager.js`](src/gameManager.js:1027) 中的水合逻辑后，游戏状态能正确加载，回合计数器也随之正常显示。之前的初步分析可能未触及根本原因（状态未能正确初始化）。
*   **原下一步**: (已无需执行)
*       1.  检查 `GameService.StartPlayerTurnAsync` 中回合数增加的逻辑和时机。
*       2.  检查 `GameState` 初始化时的 `Turn` 属性值。
*       3.  检查 `TurnStartedEventDto` 在第一个回合广播时的 `TurnNumber` 值。
*       4.  检查前端 `src/gameManager.js` 中 `handleTurnStarted` 对回合数的处理。

### BUG-011: AI 的 PP/手牌数量 UI 未更新
*   **现象**: AI 对手的 PP 值和手牌数量在 UI 上没有按预期更新。
    *   当 AI 先手时: 只有调度后的第一回合（Turn 0/实际 Turn 1）UI 会更新一次，后续回合不再更新。
    *   当 AI 后手时: UI 始终不会更新 AI 的 PP 和手牌数。
*   **触发条件**: AI 回合开始或进行中。
*   **状态**: Open
*   **优先级**: High (影响核心游戏状态信息展示)
*   **初步分析**: 可能是后端未能正确广播 AI 的 `PlayerStateUpdateEventDto` 或 `HandUpdateEventDto`，或者广播了但前端未能正确处理这些针对 AI 玩家的事件。考虑到 AI 先手第一回合能更新，可能是后续回合的事件触发或处理逻辑存在问题。可能与 BUG-012 (回合计数错误) 相关。
*   **下一步**:
    1.  检查 `GameService.StartPlayerTurnAsync` 和 `AiTurnExecutor` 中与 AI 状态更新相关的事件广播逻辑。
    2.  检查前端 `src/gameManager.js` 中 `handlePlayerStateUpdated` 和 `handleHandUpdate` 是否正确处理了来自 AI 玩家的事件。
    3.  分析 AI 先手第一回合能更新的原因，对比后续回合的差异。

### BUG-014: 结束回合后下一回合无法自动开始
*   **现象**: 在修复 BUG-011 的简单方案被回滚后，点击“结束回合”按钮，游戏流程卡住，无法自动进入下一位玩家的回合。后端日志显示仅广播了 `TurnEndedEvent`。
*   **触发条件**: 在 [`Server/Services/GameService.cs`](Server/Services/GameService.cs:221) 回滚了针对 BUG-011 的简单修复后，结束任何玩家的回合时。
*   **状态**: 已解决
*   **解决日期**: 2025-06-02
*   **解决方案**: 此问题与 BUG-010 相关，并通过 M7/M8 重构解决。确保了在 `GameHub.EndTurn` 或 AI 回合结束流程中，能正确触发 `GameService.StartPlayerTurnAsync` 来启动下一回合，并广播正确的事件。具体修复参考 [`文档/临时/Bug修复计划_Hydrate与TurnEnd_V1.0.md`](文档/临时/Bug修复计划_Hydrate与TurnEnd_V1.0.md)。
*   **发现日期**: 2025-06-01
*   **优先级**: Critical (完全阻塞游戏流程)
*   **根本原因**: 回滚操作移除了 [`GameService.EndTurnAsync`](Server/Services/GameService.cs:221) 中触发下一回合开始的逻辑。同时，调用方 [`GameHub.EndTurn`](Server/Hubs/GameHub.cs:111) 在调用 [`GameService.EndTurnAsync`](Server/Services/GameService.cs:221) 后，也没有负责获取下一位玩家信息并调用 [`GameService.StartPlayerTurnAsync`](Server/Services/GameService.cs:391) 或 [`GameService.TriggerAiTurnAsync`](Server/Services/GameService.cs:2101) 来启动下一回合。
*   **修复方案**: (已解决，见解决方案)
*   **下一步**: (已解决)
### BUG-015: 双方手牌/战场 UI 仅在首个完整游戏回合后更新
*   **现象**: 修复 BUG-014 后，双方玩家的 PP 和牌堆数量能正常更新。然而，**双方玩家**的手牌和战场上的单位/法术效果仅在第一个**完整游戏回合**（即玩家 1 的 Turn 1 和 AI 的 Turn 1 都结束后）能在 UI 上看到更新。从**第二个完整游戏回合**（即玩家 1 的 Turn 2）开始，即使玩家或 AI 抽牌、出牌或单位行动，客户端 UI 也不再更新**双方**的手牌区和战场区。
*   **触发条件**: 第二个完整游戏回合（Turn 2）开始及以后。
*   **状态**: Open
*   **发现日期**: 2025-06-01 (描述修正于 2025-06-01)
*   **优先级**: Critical (严重影响核心游戏信息展示)
*   **初步分析**: BUG-014 修复后，回合流转和基础事件广播（如 PP/Deck Update）已恢复。此问题表明，与手牌和战场相关的特定事件（如 `CardDrawnEvent`, `UnitPlayedEvent`, `SpellPlayedEvent` 等）虽然可能被服务器广播并被客户端接收（因为 PP/Deck 更新正常），但在第二个完整游戏回合开始后未能触发 `src/ui.js` 中的正确渲染逻辑，且**此问题影响双方玩家**。原因可能在于：
    *   `src/gameManager.js` 在处理这些特定事件时，未能正确更新 `gameManager.player` 和 `gameManager.opponentPlayer` 的状态（例如手牌列表、战场单位列表）。
    *   `src/ui.js` 中的 `renderPlayerHand`, `renderOpponentHand` 或 `renderBoard` 函数存在逻辑错误，导致在后续回合无法正确读取或渲染更新后的双方状态。
    *   `gameManager.js` 和 `ui.js` 之间的状态同步或渲染调用机制在后续回合对双方都失效。
*   **下一步**:
    1.  切换到 `code` 模式。
    2.  读取 [`src/gameManager.js`](src/gameManager.js) 和 [`src/ui.js`](src/ui.js)。
    3.  在 `gameManager.js` 处理**双方**手牌/战场相关事件的 handler 中添加日志，记录收到的事件数据和更新前后的 `gameManager.player` 及 `gameManager.opponentPlayer` 状态。
    4.  在 `ui.js` 的 `renderPlayerHand`, `renderOpponentHand` 和 `renderBoard` 函数入口添加日志，记录传入的状态数据。
    5.  请求用户测试并提供客户端控制台日志。
    6.  分析日志，定位问题根源。

---
---
**关于 BUG-010 (AI 先手回合结束后未触发人类玩家回合开始) 的更新:**
*   **状态更新**: Closed (原始描述的现象 - AI 回合结束后游戏卡死/End Turn 按钮禁用 - 已通过移除 `SubmitMulliganAsync` 中的冗余事件广播解决。相关的新问题见 BUG-011 和 BUG-012。)

---
### BUG-010: AI 先手回合结束后未触发人类玩家回合开始
*   **现象**: AI 先手时，完成其第一个回合的动作（例如打出卡牌）后，游戏卡死。后端日志显示 AI 调用了 `EndTurnAsync`，但后续没有为人类玩家调用 `StartPlayerTurnAsync`。前端日志显示人类玩家收到了回合开始事件，但这似乎是 Mulligan 流程中不合时宜触发的，而非 AI 回合正常结束后的流程。
*   **触发条件**: AI 先手，完成第一个回合后。
*   **状态**: 已解决
*   **解决日期**: 2025-06-02
*   **解决方案**: 通过 M7/M8 重构，确保 `AiTurnExecutor.ExecuteTurnAsync` (或其调用链如 `GameService.ExecuteAiActionPhaseAsync`) 在 AI 行动完成后，正确调用 `GameService.StartPlayerTurnAsync` 来启动人类玩家回合，并广播包含完整游戏状态的 `GameStateUpdateEventDto`。具体修复参考 [`文档/临时/Bug修复计划_Hydrate与TurnEnd_V1.0.md`](文档/临时/Bug修复计划_Hydrate与TurnEnd_V1.0.md)。
*   **优先级**: Critical (阻碍核心玩法)
*   **初步分析**: (已解决，见解决方案)
*   **下一步**: (已解决)

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
*   **根本原因分析 (更新)**: 后端存在**事件广播时序冲突**。由人类玩家结束回合触发的 `GameService.EndTurn
## 2025-06-03

### BUG-018: 随从战斗反击伤害结算错误 (破坏则无反击)
*   **现象 (修正后)**: 当随从 A 攻击随从 B 时，如果随从 A 的攻击**足以破坏**随从 B，则随从 A **不会**受到来自随从 B 的反击伤害。
*   **触发条件**: 随从攻击另一个随从，且攻击伤害 ≥ 防御方随从当前生命值。
*   **预期行为**: 战斗伤害（攻击和反击）应同时结算。即使防御方被破坏，也应在被移除前对攻击方造成反击伤害。
*   **状态**: 待观察 (代码已修改，待实战测试)
*   **发现日期**: 2025-06-02 (提及), 2025-06-03 (澄清)
*   **优先级**: Critical (违反核心战斗规则)
*   **根本原因分析**: [`Server/Services/GameService.cs`](Server/Services/GameService.cs) 的 `AttackAsync` 方法 ([`Server/Services/GameService.cs:1301`](Server/Services/GameService.cs:1301)) 中，在应用反击伤害前，存在一个对防御方随从存活状态的检查 ([`Server/Services/GameService.cs:1434`](Server/Services/GameService.cs:1434): `if ((defendingUnit.CurrentHealth) &gt; 0)`). 如果初始攻击破坏了防御方，此检查失败，导致反击伤害被跳过。
*   **修复方案**: 移除或注释掉 [`Server/Services/GameService.cs:1434`](Server/Services/GameService.cs:1434) 的 `if ((defendingUnit.CurrentHealth) &gt; 0)` 检查，确保反击伤害在防御方是随从且攻击方存活时总能应用。
*   **下一步**:
    1.  (已完成) 更新此 BUG 描述和分析。
    2.  切换到 `code` 模式。
    3.  应用修复方案到 [`Server/Services/GameService.cs`](Server/Services/GameService.cs)。
    4.  进行测试验证。
    5.  更新状态为 `Pending Verification` 或 `Resolved`。

### BUG-019: 守护 (Ward) 关键字未正确阻止攻击
*   **现象**: (已确认为 UI 问题) 当敌方场上存在守护随从时，己方随从尝试攻击时，UI 仍错误地高亮显示非守护单位为可攻击目标，尽管服务器会正确阻止该攻击并提示。
*   **触发条件**: 敌方场上存在守护随从时，在 UI 上选择己方随从准备攻击。
*   **状态**: Closed (UI 问题，由 BUG-020 追踪)
*   **发现日期**: 2025-06-02 (提及), 2025-06-03 (澄清为 UI 问题)
*   **优先级**: N/A (见 BUG-020)
*   **分析**: 服务器端 `AttackAsync` 方法中的守护验证逻辑 ( [`Server/Services/GameService.cs:1390-1402`](Server/Services/GameService.cs:1390) ) 经检查**符合预期**，能正确阻止非法攻击。此问题是客户端 UI 未能根据守护规则正确更新可攻击目标的高亮状态。
*   **下一步**: 参考 BUG-020。

### BUG-020: 守护 (Ward) 随从在 UI 上未高亮显示
*   **现象**: 1. 拥有守护关键字的随从在战场上时，其卡牌 UI 没有特殊高亮或其他视觉提示。 2. 当敌方场上存在守护随从时，己方随从尝试攻击时，UI 错误地将非守护单位高亮为可攻击目标 (即使服务器会阻止攻击)。
*   **触发条件**: 1. 守护随从在场。 2. 敌方有守护随从，己方选择随从准备攻击。
*   **状态**: Fix Applied, Pending Testing (UI 高亮逻辑已修正于 [`src/ui.js`](src/ui.js))
*   **发现日期**: 2025-06-02 (提及), 2025-06-03 (合并现象 2)
*   **优先级**: High (现象 2 严重影响可玩性)
*   **初步分析**: 问题在于客户端 UI 逻辑。需要检查 [`src/ui.js`](src/ui.js) 或相关模块中：1. 负责渲染随从 UI 的部分，确保能根据 `Keywords` 添加守护高亮。 2. 负责计算和高亮可攻击目标的逻辑 (可能在 `ui.js` 的攻击事件处理或 `gameManager.js` 的状态更新中)，确保在判断目标时，优先检查并仅高亮守护单位（如果存在）。后端服务器逻辑 (`AttackAsync`) 已确认能正确阻止非法攻击。
*   **下一步**: 1. (已完成) 修正 [`src/ui.js`](src/ui.js) 中 `renderAttackHighlights` 的逻辑。 2. 测试验证修复效果。 3. 更新状态。

### BUG-021: AI 攻击时 UI 未更新主战者生命值
*   **现象**: AI 攻击玩家主战者时，UI 上浮动的伤害数字正常显示，但主战者的生命值数字没有刷新。随从之间的战斗血量刷新正常。
*   **触发条件**: AI 攻击玩家主战者。
*   **状态**: Fix Applied (Server), Ready for Testing
*   **发现日期**: 2025-06-03
*   **优先级**: High (影响核心信息展示)
*   **根本原因分析**: 服务器端 [`Server/Services/GameService.cs`](Server/Services/GameService.cs) 中的 `ApplyDamage` 方法在生成伤害/生命值变化事件时，对目标是主战者的情况 (`PlayerStateDto`) 使用了错误的 UID (`PlayerId` 而非 `LeaderCard.InstanceId`)。
*   **修复方案 (修订)**: 虽然之前的修复 ([`Server/Services/GameService.cs:1876`](Server/Services/GameService.cs:1876)) 修正了事件中的 UID，但根本问题在于数据冗余。将执行一次重构，统一玩家生命值表示。计划详情记录在 [`文档/技术文档/M8_领域模型重构计划_V1.0.md`](文档/技术文档/M8_领域模型重构计划_V1.0.md) 的第 7 节。
*   **下一步**: 1. (已完成) 在相关文档中记录重构计划。 2. 获取用户对计划的确认和关于 `PlayerState.cs` 的澄清。 3. 执行编码。 4. 测试验证。 5. 更新文档和此 BUG 状态。