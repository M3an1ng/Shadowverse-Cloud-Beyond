# 文档/AI 设计文档 - 攻击逻辑 V0.1

## 1. 目标

实现基础的 AI 攻击决策逻辑，使 AI 控制的玩家（player2）能够利用其场上可攻击的随从，向敌方（player1）的有效目标（随从或主战者）发起攻击。

## 2. 触发时机

AI 的攻击决策逻辑将在 AI 回合的主阶段（`Main` phase）执行，位于出牌逻辑 (`aiDecideAndPlayCard`) 完成之后，结束回合 (`endTurn`) 之前。具体位置在 [`src/ai.js`](src/ai.js:14) 的 `runAILogic` 函数内，当前标记为 `TODO: 攻击决策` 的位置 (Line 37-39)。

```javascript
// src/ai.js - runAILogic 示例
export function runAILogic(currentGameState) {
  // ... 获取 aiPlayer ...
  try {
    // 1. 出牌决策
    aiDecideAndPlayCard(aiPlayer, currentGameState);

    // ===> 2. 攻击决策 (在此处调用新的攻击逻辑) <===
    console.log("AI: Deciding attacks...");
    const opponentPlayer = currentGameState.players.find(
      (p) => p && p.id !== aiPlayer.id
    );
    if (opponentPlayer) {
      aiDecideAndAttack(aiPlayer, opponentPlayer, currentGameState); // 新增调用
    } else {
      console.error("AI Attack Error: Could not find opponent player state.");
    }

    // 3. TODO: 进化决策

    // 4. 结束回合
    console.log("AI: Actions processing finished. Ending turn shortly.");
    setTimeout(endTurn, 1000);
  } catch (error) {
    // ... 错误处理 ...
  }
}
```

## 3. 核心逻辑 (V0.1 - 简单贪心策略)

AI 将遵循以下步骤进行攻击决策：

1.  **获取可攻击者:** 遍历 AI 自身战场 (`aiPlayer.field`)，筛选出所有 `canAttack === true` 的随从。
2.  **迭代攻击:** 对于每一个可攻击的随从（按战场顺序或其他简单顺序）：
    a. **获取有效目标:** 查找对手 (`opponentPlayer`) 的所有潜在目标：对手主战者 (`opponentPlayer.leaderCard`) 和对手战场上的所有随从 (`opponentPlayer.field`)。
    b. **应用守护规则:** 检查对手战场是否存在带有【守护 (Ward)】关键字的随从。
    _ **是:** 将有效目标列表过滤为仅包含带有【守护】的敌方随从。
    _ **否:** 保留所有潜在目标。
    c. **选择目标 (简单优先级):**
    _ 如果存在带有【守护】的有效目标，优先选择其中**生命值最低**的一个。
    _ 如果没有【守护】目标，但在步骤 b 中有其他随从目标，则选择其中**生命值最低**的一个敌方随从。
    _ 如果没有敌方随从可作为目标（或已被守护阻挡），则选择敌方主战者作为目标。
    _ 如果没有任何有效目标，则此攻击者跳过。
    d. **执行攻击:** 如果找到了目标，则调用 `gameManager` 的攻击执行函数（见第 5 节）来执行这次攻击。**注意：** `gameManager` 会处理攻击后 `canAttack` 变为 `false` 的逻辑。
    e. **继续下一个攻击者:** 处理列表中的下一个可攻击随从，重复步骤 a-d。

**注意:** 这个 V0.1 策略非常基础，不考虑攻击力交换、消灭高威胁目标、保留攻击次数等复杂情况。后续版本可以迭代优化。

## 4. 函数设计

将在 [`src/ai.js`](src/ai.js:0) 文件中新增 `aiDecideAndAttack` 函数：

```javascript
// src/ai.js (新增函数)

import { executeAttack } from "./gameManager.js"; // <--- 需要从 gameManager 导入 executeAttack

/**
 * AI 决定并执行所有可能的攻击
 * @param {object} aiPlayer - 当前 AI 玩家的状态对象
 * @param {object} opponentPlayer - 对手玩家的状态对象
 * @param {object} gameState - 当前游戏状态 (可能需要，例如获取回合数等)
 */
function aiDecideAndAttack(aiPlayer, opponentPlayer, gameState) {
  console.log(`AI Attack Phase: ${aiPlayer.id} starting attack decisions.`);

  // 1. 获取可攻击者
  const attackers = aiPlayer.field.filter((f) => f && f.canAttack === true);
  if (attackers.length === 0) {
    console.log("AI Attack Phase: No followers can attack.");
    return; // 没有可攻击的随从
  }
  console.log(
    `AI Attack Phase: Found ${attackers.length} potential attackers:`,
    attackers.map((a) => a.name)
  );

  // 2. 迭代攻击
  for (const attacker of attackers) {
    // 重新检查 canAttack，因为之前的攻击可能改变了状态（虽然 gameManager 应该处理，但双重检查更安全）
    if (!attacker.canAttack) {
      console.log(
        `AI Attack Check: Attacker ${attacker.name} cannot attack anymore this turn.`
      );
      continue;
    }

    // a. 获取潜在目标
    let potentialTargets = [];
    if (opponentPlayer.leaderCard) {
      potentialTargets.push(opponentPlayer.leaderCard);
    }
    potentialTargets = potentialTargets.concat(opponentPlayer.field);

    if (potentialTargets.length === 0) {
      console.log(
        `AI Attack: No potential targets found for ${attacker.name}.`
      );
      continue; // 没有目标可以攻击
    }
    console.log(
      `AI Attack: Potential targets for ${attacker.name}:`,
      potentialTargets.map((t) => `${t.name}(${t.type})`)
    );

    // b. 应用守护规则
    const opponentHasWard = opponentPlayer.field.some(
      (f) => f && f.keywords?.includes("Ward")
    );
    let validTargets = potentialTargets;

    if (opponentHasWard) {
      console.log(`AI Attack: Opponent has Ward. Filtering targets...`);
      validTargets = potentialTargets.filter(
        (t) =>
          (t.type === "Follower" || t.type === "Follower_Evolved") &&
          t.keywords?.includes("Ward")
      );
      console.log(
        `AI Attack: Ward targets for ${attacker.name}:`,
        validTargets.map((t) => t.name)
      );
      if (validTargets.length === 0) {
        console.log(
          `AI Attack: No valid Ward targets found for ${attacker.name}, cannot bypass Ward.`
        );
        continue; // 如果有守护但找不到守护目标，则此攻击者无法攻击任何其他目标
      }
    } else {
      // No Ward, all potential targets are valid initially
      validTargets = potentialTargets.filter(
        (t) =>
          t.type === "Follower" ||
          t.type === "Follower_Evolved" ||
          t.type === "Leader"
      ); //确保只包含有效类型
      console.log(
        `AI Attack: No Ward detected. Valid targets for ${attacker.name}:`,
        validTargets.map((t) => `${t.name}(${t.type})`)
      );
    }

    if (validTargets.length === 0) {
      console.log(
        `AI Attack: No valid targets remain for ${attacker.name} after filtering.`
      );
      continue; // 没有可选目标
    }

    // c. 选择目标 (最低血量优先)
    let bestTarget = null;
    let minHealth = Infinity;

    // 优先攻击随从 (守护或非守护)
    const followerTargets = validTargets.filter(
      (t) => t.type === "Follower" || t.type === "Follower_Evolved"
    );
    if (followerTargets.length > 0) {
      bestTarget = followerTargets.reduce((minHpTarget, currentTarget) => {
        return currentTarget.currentHealth <
          (minHpTarget?.currentHealth ?? Infinity)
          ? currentTarget
          : minHpTarget;
      }, null);
      console.log(
        `AI Attack Target Selection: Chose follower target ${bestTarget?.name} (HP: ${bestTarget?.currentHealth}) for ${attacker.name}.`
      );
    } else {
      // 没有随从目标，则选择主战者 (如果它是有效目标的话)
      bestTarget = validTargets.find((t) => t.type === "Leader");
      if (bestTarget) {
        console.log(
          `AI Attack Target Selection: No follower targets available. Chose leader target ${bestTarget?.name} (HP: ${bestTarget?.currentHealth}) for ${attacker.name}.`
        );
      }
    }

    // d. 执行攻击
    if (bestTarget) {
      console.log(
        `AI EXECUTE ATTACK: ${attacker.name} (UID: ${attacker.uid}) attacking ${bestTarget.name} (UID: ${bestTarget.uid})`
      );
      try {
        // 调用 gameManager 的 executeAttack 来处理实际的攻击逻辑和状态更新
        // !! 关键：确保 executeAttack 在 gameManager 中是导出的 (export) !!
        // 并且 handleDestruction, checkGameOver 等相关函数也能被 executeAttack 正确调用
        // gameManager 的 executeAttack 应该处理完伤害计算、破坏检查、游戏结束检查，并更新攻击者的 canAttack = false
        executeAttack(attacker.uid, bestTarget.uid);
        console.log(
          `AI Attack SUCCESS: ${attacker.name} attacked ${bestTarget.name}.`
        );
        // 无需在这里手动设置 attacker.canAttack = false，因为 gameManager.executeAttack 应该处理
      } catch (error) {
        console.error(
          `AI Attack ERROR executing attack from ${attacker.name} to ${bestTarget.name}:`,
          error
        );
        // 如果攻击执行失败，是否应该停止后续攻击？ V0.1 简单处理，继续尝试下一个攻击者
      }
    } else {
      console.log(`AI Attack: No target chosen for ${attacker.name}.`);
    }
    // 攻击完成后，不需要 break，继续处理下一个可攻击的随从
    console.log(
      `AI Attack Phase: Finished decisions for attacker ${attacker.name}. Checking next attacker...`
    );
  } // end for loop iterating through attackers

  console.log(
    `AI Attack Phase: ${aiPlayer.id} finished all attack decisions for this turn.`
  );
}
```

## 5. 与 `gameManager.js` 的交互

AI 的攻击逻辑将**依赖并调用** [`src/gameManager.js`](src/gameManager.js:0) 中现有的 `executeAttack(attackerUid, targetUid)` 函数 (Line 777)。

**前提条件:**

1.  `executeAttack` 函数必须从 `gameManager.js` 中 **导出 (export)**，以便 `ai.js` 可以导入。
2.  `executeAttack` 函数内部必须完整处理伤害计算、随从破坏（调用 `handleDestruction`）、游戏结束检查（调用 `checkGameOver`）以及**将攻击者的 `canAttack` 状态设置为 `false`**。

这样做的好处是最大程度地复用了现有的、经过测试（或将要测试）的核心战斗逻辑，避免了在 AI 代码中重复实现复杂的战斗结算和状态更新，降低了出错的可能性。

AI 只需要负责**决策**（选哪个打哪个），而不需要负责**执行**的具体细节。

## 6. 可视化流程 (Mermaid)

```mermaid
graph TD
    A[开始 AI 攻击决策: aiDecideAndAttack] --> B{获取所有 canAttack 的己方随从};
    B --> C{是否有可攻击随从?};
    C -- 否 --> D[结束攻击决策];
    C -- 是 --> E[选择一个可攻击随从];
    E --> F{获取所有潜在敌方目标 (随从+主战者)};
    F --> G{对手是否有守护随从?};
    G -- 是 --> H[过滤目标: 只保留守护随从];
    G -- 否 --> I[保留所有有效目标];
    H --> J{过滤后是否有目标?};
    I --> J;
    J -- 否 --> K[此攻击者结束，选择下一个];
    J -- 是 --> L{选择目标 (优先打最低血量守护/随从)};
    L --> M{是否找到目标?};
    M -- 否 --> K;
    M -- 是 --> N[调用 gameManager.executeAttack(攻击者UID, 目标UID)];
    N --> K;
    K --> C;
```

## 7. 后续 V0.2+ 改进方向

- 更智能的目标选择（例如，考虑攻击力交换，优先解高威胁随从，计算斩杀线）。
- 考虑法术和效果对攻击决策的影响。
- 在选择攻击者时考虑顺序（例如，先用低攻随从试探）。
- 不总是攻击，根据场面情况选择保留攻击次数。
- 与进化决策的联动。
