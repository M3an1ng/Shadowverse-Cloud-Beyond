# AI 设计文档 - 进化逻辑 V0.1

- **版本:** 0.1
- **日期:** 2025-05-24
- **作者:** 资深卡牌游戏开发专家与技术总监 (AI)
- **状态:** 已实现并通过测试 (V0.1)

## 1. 目标

为游戏 AI 添加基础的随从进化决策和执行能力。在满足条件的回合，AI 应能选择一个合适的己方随从进行进化，且每回合最多执行一次进化。

## 2. 触发时机

AI 的进化决策逻辑 (`aiDecideAndEvolve`) 应在 AI 回合的主要阶段执行。具体的调用时机是在 `runAILogic` 函数内部，完成所有出牌操作之后，执行所有攻击操作之前。

```
runAILogic(...)
  -> aiDecideAndPlayCard(...)
  -> aiDecideAndEvolve(...) // <-- 新增调用点
  -> aiDecideAndAttack(...)
  -> endTurn()
```

## 3. 核心逻辑函数

在 [`src/ai.js`](src/ai.js:0) 文件中实现一个新的导出函数：

```javascript
/**
 * AI 决策并执行进化操作 (V0.1)
 * @param {object} aiPlayer - AI 玩家状态对象
 * @param {object} currentGameState - 当前游戏状态对象 (包含对手信息)
 * @returns {boolean} 如果成功执行了进化则返回 true, 否则返回 false
 */
export function aiDecideAndEvolve(aiPlayer, currentGameState) {
  // 签名与实际代码保持一致
  // ... 实现 V0.1 决策流程 ...
}
```

## 4. 决策流程 (V0.1 - 简单优先)

`aiDecideAndEvolve` 函数将按以下步骤执行：

1.  **检查全局进化权限:** 检查 `aiPlayer.canEvolveThisTurn` 是否为 `true`。若为 `false`，则日志记录无法进化原因，并返回 `false`。
2.  **遍历己方战场:** 使用 `for...of` 循环遍历 `aiPlayer.field` 数组中的每个随从实例 `cardInstance`。
3.  **检查随从资格:**
    - **是否已进化:** 检查 `!cardInstance.isEvolved`。如果已进化，跳过此随从。
    - **获取定义:** 调用 `getCardDefinition(cardInstance.id)` 获取卡牌基础定义 `definition`。
    - **是否允许进化:** 检查 `definition && definition.can_evolve === true`。如果定义不存在或不允许进化，跳过此随从。
4.  **选择目标 (V0.1 策略):** 如果一个随从通过了上述所有检查，则它就是本次的进化目标 (`targetFollower`)。**立即停止遍历** (例如使用 `break`)。
5.  **执行进化:** 如果找到了 `targetFollower`：
    - 记录 AI 决策日志，说明选择了哪个随从进行进化。
    - 调用 `evolveCard(targetFollower.uid)` （需要从 `gameManager.js` 导入）。
    - 记录调用结果（成功或失败，理论上预检查通过后应总是成功）。
    - 函数返回 `true`。
6.  **未找到目标:** 如果遍历完战场都没有找到合适的进化目标，记录日志说明未找到，函数返回 `false`。

**关键约束:** V0.1 版本每回合最多只进化**一个**随从。

## 5. Mermaid 流程图

```mermaid
flowchart TD
    A[开始 aiDecideAndEvolve] --> B{aiPlayer.canEvolveThisTurn == true?};
    B -- 否 --> B_NO[记录: 本回合无法进化] --> F[返回 false];
    B -- 是 --> C[遍历 aiPlayer.field 中的随从 cardInstance];
    C --> D{找到下一个随从?};
    D -- 是 --> E{cardInstance 未进化?};
    D -- 否 --> D_NO[记录: 未找到可进化目标] --> F;
    E -- 否 --> C;
    E -- 是 --> G[获取 cardDefinition = getCardDefinition(cardInstance.id)];
    G --> H{definition 存在且 can_evolve == true?};
    H -- 否 --> C;
    H -- 是 --> I[找到目标! targetFollower = cardInstance];
    I --> J[记录: 决定进化 targetFollower];
    J --> K[调用 evolveCard(targetFollower.uid)];
    K --> L[记录: 进化调用结果];
    L --> M[返回 true];
```

## 6. 代码实现细节

- **目标文件:** [`src/ai.js`](src/ai.js:0)
- **新增函数:** `aiDecideAndEvolve(aiPlayer, opponentPlayer, gameState)`
- **修改函数:** `runAILogic(gameState)` - 在 `aiDecideAndPlayCard` 之后，`aiDecideAndAttack` 之前添加对 `aiDecideAndEvolve` 的调用。

  ```javascript
  // 伪代码 runAILogic
  function runAILogic(gameState) {
    const aiPlayer = gameState.activePlayer;
    const opponentPlayer = gameState.opponentPlayer;

    // 1. 出牌逻辑 (已存在)
    aiDecideAndPlayCard(aiPlayer, currentGameState); // 注意：保持签名一致性

    // 2. 进化逻辑 (新增)
    aiDecideAndEvolve(aiPlayer, currentGameState); // 调用新函数

    // 3. 攻击逻辑 (已存在)
    aiDecideAndAttack(aiPlayer, currentGameState); // 注意：保持签名一致性

    // 4. 结束回合 (已存在)
    console.log("AI actions complete. Ending turn.");
    endTurn(); // Assuming endTurn is available or imported
  }
  ```

- **所需导入 (在 [`src/ai.js`](src/ai.js:0) 顶部):**
  ```javascript
  // ... 其他导入 ...
  import { evolveCard } from "./gameManager.js";
  import { getCardDefinition } from "./cardManager.js";
  // 确保 endTurn 也被导入 (如果尚未导入)
  // import { endTurn } from './turnManager.js'; // 可能已存在
  ```

## 7. 测试策略

1.  **环境准备:**
    - 修改 [`data/card_data.json`](data/card_data.json:0)，确保至少有一个随从的 `can_evolve` 属性为 `true`。
    - 配置 AI 的卡组，使其包含可进化的随从。
2.  **状态设置 (可能需要管理员后台/调试命令):**
    - 将游戏推进到 AI 玩家的第 4 或第 5 回合（根据先后手），确保 `hasUnlockedEvolution` 为 `true`。
    - 确保 AI 玩家有 EP (`ep > 0`)。
    - 确保 AI 战场上有一个或多个满足进化条件的随从实例（未进化、定义允许）。
3.  **执行与观察:**
    - 开始 AI 回合。
    - 观察控制台日志：
      - 确认 `aiDecideAndEvolve` 被调用。
      - 确认日志显示 AI 检查了 `canEvolveThisTurn`。
      - 确认日志显示 AI 遍历了战场随从并检查了条件。
      - 确认日志显示 AI 选择了**第一个**符合条件的随从。
      - 确认 `evolveCard` 被成功调用。
      - 确认进化结果反映在游戏状态和 UI 上（EP 减少，随从属性变化，`isEvolved` 变为 `true`）。
4.  **边界测试:**
    - 测试 AI 无 EP 时不进化。
    - 测试 AI 未到解锁回合时不进化。
    - 测试 AI 战场上只有不可进化/已进化随从时不进化。
    - 测试 AI 场上有多个可进化随从时，只进化了第一个。

- **(2025-05-24) V0.1 测试通过:** 已根据上述策略进行测试，AI 进化功能符合预期。

## 8. 未来改进方向 (V0.2+)

- **智能目标选择:** 不再选择第一个，而是根据启发式规则评估所有可进化随从的价值（如：攻击力、生命值、特殊效果、能否立即攻击等），选择价值最高的进行进化。
- **EP 保留策略:** AI 可以根据场面情况决定是否要保留 EP 用于未来的关键回合或效果。
- **多步进化:** 如果游戏机制允许（影之诗不允许），考虑多次进化。
