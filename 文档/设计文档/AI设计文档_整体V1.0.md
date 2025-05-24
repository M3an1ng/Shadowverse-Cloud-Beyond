# AI 设计文档 - 整体逻辑 V1.0

- **版本:** 1.0 (合并 V0.1 攻击与进化逻辑)
- **日期:** 2025-05-24
- **作者:** 资深卡牌游戏开发专家与技术总监 (AI)
- **状态:** 已合并设计，对应代码已实现 V0.1 功能

## 1. 总体目标

为游戏 AI 提供基础的决策能力，使其能够在一个回合内执行出牌、进化和攻击操作，模拟一个有基本策略的对手。

## 2. AI 回合执行流程 (`src/ai.js` - `runAILogic`)

AI 的主要逻辑在 `runAILogic` 函数中按顺序执行：

1.  **获取状态:** 获取当前 AI 玩家 (`aiPlayer`) 和对手玩家 (`opponentPlayer`) 的状态。
2.  **出牌决策 (`aiDecideAndPlayCard`):** AI 决定并尝试打出手牌。(逻辑细节待补充或引用其他文档)
3.  **进化决策 (`aiDecideAndEvolve`):** AI 检查是否满足进化条件，并选择一个随从进行进化（每回合最多一次）。
4.  **攻击决策 (`aiDecideAndAttack`):** AI 使用场上可攻击的随从选择目标并发起攻击。
5.  **结束回合:** 调用 `endTurn`。

```javascript
// 伪代码 src/ai.js - runAILogic 流程
export function runAILogic(currentGameState) {
  const aiPlayer = /* ... 获取 AI 玩家 ... */;
  const opponentPlayer = /* ... 获取对手玩家 ... */;

  try {
    // 1. 出牌决策 (逻辑待详述)
    console.log("AI: Deciding plays...");
    aiDecideAndPlayCard(aiPlayer, currentGameState); // 已有函数签名

    // 2. 进化决策 (V0.1)
    console.log("AI: Deciding evolution...");
    aiDecideAndEvolve(aiPlayer, currentGameState); // 新增调用

    // 3. 攻击决策 (V0.1)
    console.log("AI: Deciding attacks...");
    aiDecideAndAttack(aiPlayer, opponentPlayer, currentGameState); // 新增调用

    // 4. 结束回合
    console.log("AI: Actions processing finished. Ending turn shortly.");
    setTimeout(endTurn, 1000); // 假设 endTurn 可用
  } catch (error) {
    // ... 错误处理 ...
  }
}
```

## 3. 进化决策逻辑 (V0.1 - 简单优先) - `aiDecideAndEvolve`

**目标:** 每回合在满足条件时进化一个符合资格的随从。

**核心流程:**

1.  检查全局进化权限 (`aiPlayer.canEvolveThisTurn`)。
2.  遍历己方战场，找到**第一个**满足以下条件的随从：
    - 未进化 (`!cardInstance.isEvolved`)。
    - 卡牌定义存在且允许进化 (`definition.can_evolve === true`)。
3.  如果找到，调用 `gameManager.evolveCard(targetFollower.uid)` 执行进化，并返回 `true`。
4.  如果未找到或无法进化，返回 `false`。

**流程图:**

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

**依赖:**

- `src/gameManager.js`: `evolveCard` (需导出)
- `src/cardManager.js`: `getCardDefinition` (需导出)

## 4. 攻击决策逻辑 (V0.1 - 简单贪心) - `aiDecideAndAttack`

**目标:** 利用所有可攻击随从，按简单优先级选择目标进行攻击。

**核心流程:**

1.  获取所有 `canAttack === true` 的己方随从 (`attackers`)。
2.  对每个 `attacker`:
    a. 获取所有潜在敌方目标（主战者 + 随从）。
    b. 检查对手是否有【守护】随从。
    c. 如果有【守护】，目标限定为【守护】随从；否则目标为所有敌方单位。
    d. 如果有有效目标：
    i. 优先选择目标中的**最低血量随从**（守护优先于非守护）。
    ii. 如果没有随从目标，则选择敌方主战者。
    e. 如果选定目标，调用 `gameManager.executeAttack(attacker.uid, target.uid)`。

**流程图:**

```mermaid
graph TD
    AA[开始 AI 攻击决策: aiDecideAndAttack] --> BB{获取所有 canAttack 的己方随从};
    BB --> CC{是否有可攻击随从?};
    CC -- 否 --> DD[结束攻击决策];
    CC -- 是 --> EE[选择一个可攻击随从];
    EE --> FF{获取所有潜在敌方目标 (随从+主战者)};
    FF --> GG{对手是否有守护随从?};
    GG -- 是 --> HH[过滤目标: 只保留守护随从];
    GG -- 否 --> II[保留所有有效目标];
    HH --> JJ{过滤后是否有目标?};
    II --> JJ;
    JJ -- 否 --> KK[此攻击者结束，选择下一个];
    JJ -- 是 --> LL{选择目标 (优先打最低血量守护/随从)};
    LL --> MM{是否找到目标?};
    MM -- 否 --> KK;
    MM -- 是 --> NN[调用 gameManager.executeAttack(攻击者UID, 目标UID)];
    NN --> KK;
    KK --> CC;
```

**依赖:**

- `src/gameManager.js`: `executeAttack` (需导出，且内部处理守护逻辑、伤害结算、状态更新如 `canAttack=false`)

## 5. 未来改进方向 (V0.2+)

- **出牌决策:** (待细化)
- **进化决策:** 更智能的目标选择（基于价值评估），考虑保留 EP。
- **攻击决策:** 更智能的目标选择（攻击力交换、威胁评估、斩杀计算），保留攻击次数策略。
- **整体联动:** 协调出牌、进化、攻击决策，实现更复杂的组合策略。
