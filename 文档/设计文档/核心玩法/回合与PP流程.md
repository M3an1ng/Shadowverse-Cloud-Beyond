# 核心回合流程与 PP 机制 (修订版 v1.1)

## 概述

本文档定义了游戏的基本回合结构以及核心资源 Play Point (PP) 的获取和回复机制，参考了《影之诗》的标准规则，并考虑了潜在的“最大 PP 增加时”触发效果。

## 回合阶段流程图 (v1.1)

```mermaid
graph TD
    subgraph 游戏流程
        Init[游戏初始化\n(设定 P1 为先手, 最大PP=0, 当前PP=0)] --> StartTurnP1[P1 回合开始 (Turn 1)];
        StartTurnP1 --> ProcessTurnStartP1[P1 回合开始动作\n- **最大PP+1 (->1)**\n- 当前PP回满 (->1)\n- 抽1张卡\n- (处理回合开始效果)\n- 阶段 -> Main];
        ProcessTurnStartP1 --> MainPhaseP1[P1 主阶段 (1 PP 可用)\n(可以打出1费卡)];
        MainPhaseP1 -- 点击结束 --> EndTurnP1[P1 回合结束动作\n- (处理回合结束效果)\n- 切换到P2];

        EndTurnP1 --> StartTurnP2[P2 回合开始 (Turn 1)];
        StartTurnP2 --> ProcessTurnStartP2[P2 回合开始动作\n- **最大PP+1 (->1)**\n- 当前PP回满 (->1)\n- 抽2张卡 (后手福利)\n- (处理回合开始效果)\n- 阶段 -> Main];
        ProcessTurnStartP2 --> MainPhaseP2[P2 主阶段 (1 PP 可用)];
        MainPhaseP2 -- 点击结束/AI自动 --> EndTurnP2[P2 回合结束动作\n- (处理回合结束效果)\n- 切换到P1\n- 回合数 -> 2];

        EndTurnP2 --> StartTurnP1_T2[P1 回合开始 (Turn 2)];
        StartTurnP1_T2 --> ProcessTurnStartP1_T2[P1 回合开始动作\n- **最大PP+1 (->2)**\n- 当前PP回满 (->2)\n- 抽1张卡\n- (处理回合开始效果)\n- 阶段 -> Main];
        ProcessTurnStartP1_T2 --> MainPhaseP1_T2[P1 主阶段 (2 PP 可用)];
        MainPhaseP1_T2 --> MoreTurns(... 后续回合 PP 继续增长);
    end
```

## PP (Play Point) 规则详解 (v1.1)

1.  **最大 PP (Max PP):**

    - 游戏**初始化 (`initGame`)** 时，双方玩家的 **最大 PP 上限** 和 **当前 PP** 均设置为 **0**。
    - 在**每个**己方回合**开始时 (`startTurn`)**，如果玩家当前的 **最大 PP 上限** 小于 10，则**该上限会 +1**。
    - 这个增加事件发生在回合开始阶段的其他动作（如抽卡、PP 回复）之前，允许潜在的“最大 PP 增加时”效果触发。
    - 最大 PP 上限达到 10 后不再增加。

2.  **当前 PP (Current PP):**
    - 在**每个**己方回合开始时，在处理完最大 PP 上限增加（如果发生）之后，玩家的 **当前 PP** 会 **完全回复**，数值等于该回合**新的最大 PP 上限**。
    - 打出卡牌会消耗等同于卡牌费用的当前 PP。
    - 当前 PP 不能超过该回合的最大 PP 上限。

## 代码实现关联

- 初始 PP 设置 (设为 0): [`src/gameManager.js`](d:\workspace\Shadowverse-beyond\src\gameManager.js:1) 中的 `initGame` 函数。
- 回合开始 PP 增加 (每次都尝试) 与回复: [`src/turnManager.js`](d:\workspace\Shadowverse-beyond\src\turnManager.js:1) 中的 `startTurn` 函数。
- PP 消耗: [`src/gameManager.js`](d:\workspace\Shadowverse-beyond\src\gameManager.js:1) 中的 `playCard` (及未来可能的其他动作) 函数。
