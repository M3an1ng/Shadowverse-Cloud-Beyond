# Battle.cs 核心流程分析与设计思想 V1.0

## 1. 引言

本文档旨在深入分析项目核心战斗逻辑控制器 [`Scripts/Game/Battle/Battle.cs`](./Scripts/Game/Battle/Battle.cs:0) 的内部工作流程，特别是玩家动作的处理、效果的触发与执行机制。通过结合项目中的 [`文档/AI_Function_Reference.md`](./文档/AI_Function_Reference.md:0) 和 [`文档/Card_System_TDD_V1.0.md`](./文档/Card_System_TDD_V1.0.md:0) 文档，本文将阐述其关键的设计思想，并使用 Mermaid 图表可视化核心处理流程。

**目标读者:** 需要理解游戏底层战斗逻辑和事件处理机制的开发者及 AI 工具。

## 2. `Battle.cs` 核心职责

[`Battle.cs`](./Scripts/Game/Battle/Battle.cs:0) 类是整个卡牌对战逻辑的核心枢纽，其主要职责包括：

*   **状态管理:** 持有并管理当前的战斗状态 `CurrentState` ([`BattleState`](./Scripts/Game/Battle/BattleState.cs) 实例)，该状态包含了对战双方的所有信息（生命值、PP、手牌、战场、牌库等）。
*   **动作处理:** 接收并处理来自本地玩家或网络同步的玩家动作 (`PlayerAction`)。
*   **队列维护:**
    *   **效果队列 (`effectQueue`):** 使用优先级队列 (`PriorityQueue<Effect>`) 存储待处理的效果实例，根据效果的优先级 (`priority`) 和触发阶段 (`EffectPhase`) 控制执行顺序。
    *   **玩家动作队列 (`playerActionQueue`):** 使用优先级队列存储在效果处理期间接收到的玩家动作，确保它们在效果处理完毕后按实例 ID (`instanceId`) 顺序执行，避免并发冲突和保证网络同步的顺序性。
*   **流程控制:** 驱动效果队列的处理 (`ProcessQueue`)，确保效果按正确的顺序和逻辑应用。
*   **网络协调:** 与 [`BattleManager`](./Scripts/Manager/BattleManager.cs:0) 交互，发送本地玩家动作到网络 (`Hud.EnemyPlayerAction`)，并接收处理网络同步过来的动作。
*   **初始化:** 负责根据传入的卡组信息 (`BattleDeck`) 和设置 (`BattleSettings`) 初始化战斗状态 (`Init` 方法)。

## 3. 玩家动作处理 (`PlayerAction`)

[`PlayerAction(int[] data, bool isMe, int instanceId = -1)`](./Scripts/Game/Battle/Battle.cs:66) 方法是处理所有玩家输入的入口。

*   **输入参数:**
    *   `data (int[])`: 包含动作信息的整数数组。通常 `data[0]` 是 `EffectAbility` 枚举值，代表动作类型（如出牌、攻击、结束回合），后续元素是相关参数（如卡牌 ID、目标 ID）。
    *   `isMe (bool)`: 标识该动作是由本地玩家发起 (`true`) 还是从网络接收到的对手动作 (`false`)。
    *   `instanceId (int)`: 动作的唯一标识符，用于保证网络同步时动作的执行顺序。本地发起的动作会递增 `instanceId`，接收到的动作需要匹配预期的 `instanceId`。

*   **处理流程:**
    1.  **并发处理 (动作入队):** 如果当前效果队列 (`effectQueue`) 不为空（即正在处理效果），则将接收到的玩家动作（连同 `isMe` 和 `instanceId`）封装成 `Tuple` 并加入 `playerActionQueue`。这样可以防止新的玩家动作打断正在进行的效果处理链，保证逻辑的原子性和顺序性。动作会等到 `ProcessQueue` 处理完所有效果后再被取出执行。
    2.  **网络同步 (本地动作):** 如果 `isMe` 为 `true`：
        *   对 `data` 中的目标 ID 进行转换（因为双方视角不同，ID 编号规则可能相反）。
        *   调用 `Hud.EnemyPlayerAction(enemyData, instanceId)` 将处理后的动作数据通过 `BattleManager` 发送给对手。
    3.  **网络同步 (远程动作) / 顺序检查:** 如果 `isMe` 为 `false`：
        *   检查传入的 `instanceId` 是否与本地预期的 `instanceId` 匹配。如果不匹配（意味着可能之前的某个网络包还未处理或丢失），则将该动作加入 `playerActionQueue` 等待。
        *   如果匹配，则本地 `instanceId` 递增，准备处理下一个动作。
    4.  **动作转化为效果:** 将传入的 `data` 包装成一个临时的 `Effect` 对象。设置效果的来源 (`source` 通常是玩家领袖) 和发起单位 (`invokeUnit`)。
    5.  **效果入队:** 调用 `EnqueueEffect(effect)` 将这个代表玩家动作的初始效果加入效果队列。
    6.  **启动处理:** 如果效果队列原本为空（即当前没有正在处理的效果），则立即调用 `ProcessQueue()` 开始处理。

## 4. 效果队列与处理 (`EnqueueEffect`, `ProcessQueue`)

这是 `Battle.cs` 最核心的部分，负责管理和执行游戏中所有的卡牌效果。

*   **`EnqueueEffect(Effect effect, EffectPhase phase = EffectPhase.On)`:**
    *   将一个 `Effect` 实例添加到 `effectQueue`。
    *   **优先级计算:** 关键在于 `priority` 的计算逻辑。效果的最终优先级不仅取决于其基础设定，还受到其来源效果 (`sourceEffect`) 的优先级、层级 (`level`) 以及触发阶段 (`phase`) 的影响。
        *   `effect.level = effect.sourceEffect.level + 1;`: 标记效果的嵌套层级。
        *   `effect.phase = phase;`: 设置效果的触发阶段（On, Before, After）。
        *   `effect.priority = (sourcePriority * Mathf.Pow(10, effect.sourceEffect.level) + (int)phase) / 10;`: 这个公式表明，子效果的优先级会基于父效果的优先级，并结合自身的触发阶段进行微调。高层级（嵌套深）的效果似乎优先级更高（取决于 `Mathf.Pow` 的基数是否大于1），同一层级内不同阶段的效果也有先后顺序。`PriorityQueue` 会根据这个计算出的 `priority` 值（比较器设置为值越大优先级越高）来决定效果的出队顺序。
    *   这个机制保证了复杂的效果连锁（一个效果触发另一个效果）能按照预期的、有层次的顺序执行。

*   **`ProcessQueue()`:**
    *   **循环处理:** 只要效果队列不为空，就持续循环。
    *   **游戏结束检查:** 每次循环开始时，检查 `CurrentState.result.masterState` 是否已有结果，如果有则停止处理并通知 `BattleManager`。
    *   **效果出队:** 从 `effectQueue` 中取出当前优先级最高的效果实例 `e`。
    *   **条件与阶段检查:** `if(e.phase != EffectPhase.After || e.Condition(CurrentState))`
        *   这是一个关键的判断。它意味着：**对于非 `After` 阶段的效果，会直接尝试应用；对于 `After` 阶段的效果，则必须先通过 `e.Condition(CurrentState)` 检查。**
        *   这与 [`文档/AI_Function_Reference.md`](./文档/AI_Function_Reference.md:129) 中描述的伪代码（先检查所有效果的 Condition）略有不同，更强调了 `After` 阶段效果的特殊性（可能需要在行动完成后再次确认条件是否满足）。
        *   `e.Condition(CurrentState)` 内部会调用 `Parser` 系统来解析 `conditionString` 并根据当前状态判断真假。
    *   **效果应用:** 如果条件满足（或非 After 阶段），则调用 `e.Apply(CurrentState)`。
        *   **封装实现:** `Effect.Apply` 方法内部封装了效果执行的完整流程，包括：
            *   目标解析 (`ResolveTargets()`，使用 `EffectTargetInfo` 和 `BattleCardFilter`)
            *   参数计算 (`Parser.UpdateEffectExpressions()`)
            *   调用具体能力实现 (`EffectAbilityHandler.ApplyEffect()`)
            *   修改 `BattleState`
            *   （可能）触发新的效果检查 (`EffectAbilityHandler.CheckTriggers`)，导致新的效果入队。
    *   **UI 同步:** 整个队列处理完毕后，调用 `Hud.ProcessQueue()` 通知 `BattleManager` 更新 UI。
    *   **处理暂存动作:** 效果队列清空后，检查 `playerActionQueue` 是否有待处理的动作，如果有，则取出第一个动作并递归调用 `PlayerAction` 处理。

## 5. 设计思想总结

[`Battle.cs`](./Scripts/Game/Battle/Battle.cs:0) 的设计体现了以下关键思想：

*   **命令队列模式 (Command Queue Pattern):** 使用 `effectQueue` 和 `playerActionQueue` 将事件（效果触发、玩家输入）转化为命令对象（`Effect`, `Tuple`），按顺序（或优先级）进行处理。这有效地管理了游戏中的异步事件流，保证了逻辑的确定性和可控性。
*   **优先级队列 (Priority Queue):** `effectQueue` 使用优先级队列，允许系统根据效果的内在优先级、触发阶段和嵌套层级动态调整处理顺序，实现了复杂的效果交互逻辑。
*   **状态驱动 (State-Driven):** 整个战斗的核心是 `BattleState`。所有动作和效果的最终目的都是读取或修改这个状态对象。
*   **事件驱动 (Event-Driven):** 虽然 `CheckTriggers` 的调用点不在 `Battle.cs` 中，但其设计是基于事件的。特定的游戏事件（回合开始、卡牌打出、攻击等）触发效果的查找和入队。
*   **关注点分离 (Separation of Concerns):** `Battle.cs` 主要负责宏观的流程控制（队列管理、网络协调、状态持有），而效果的具体逻辑（条件判断、目标筛选、能力执行）则被封装在 `Effect` 类及其依赖的系统（`Parser`, `Filter`, `EffectAbilityHandler`）中，使得结构清晰，易于扩展。
*   **网络同步与顺序保证:** 通过 `isMe` 标志、RPC 通信以及 `instanceId` 和 `playerActionQueue` 的配合，确保了 P2P 对战中双方动作和效果处理顺序的一致性。

## 6. 核心流程图 (Mermaid)

```mermaid
graph TD
    subgraph Input [动作/事件来源]
        PlayerInput[本地玩家输入 UI] --> A1(封装 PlayerAction);
        NetworkInput[网络接收对手动作 RPC] --> A1;
        GameEvent[游戏内部事件 (e.g., Turn Start, Attack)] --> TriggerCheck(触发 EffectAbilityHandler.CheckTriggers);
    end

    subgraph BattleControl [Battle.cs 核心控制]
        A1 -- isMe=true --> SendRPC[发送 RPC 给对手];
        A1 --> PA(PlayerAction 方法);
        PA --> CheckEffectQueue{EffectQueue 是否处理中?};
        CheckEffectQueue -- 是 --> EnqueuePlayerAction[加入 playerActionQueue];
        CheckEffectQueue -- 否 --> HandleActionNow{处理动作};

        HandleActionNow -- isMe=false --> CheckInstanceID{InstanceID 是否匹配?};
        CheckInstanceID -- 否 --> EnqueuePlayerAction;
        CheckInstanceID -- 是 --> IncrementInstanceID[本地 InstanceID++];
        IncrementInstanceID --> WrapActionAsEffect[封装动作为初始 Effect];
        HandleActionNow -- isMe=true --> WrapActionAsEffect;

        TriggerCheck -- 效果匹配 --> CreateEffectInstance[创建 Effect 实例];
        CreateEffectInstance --> SetEffectContext[设置效果上下文];
        SetEffectContext --> EnqueueEffect(调用 EnqueueEffect);

        WrapActionAsEffect --> EnqueueEffect;

        EnqueueEffect --> CalculatePriority[计算效果优先级/层级/阶段];
        CalculatePriority --> AddToEffectQueue[加入 effectQueue (PriorityQueue)];
        AddToEffectQueue --> CheckAndStartProcess{如果队列原为空, 调用 ProcessQueue};

        StartProcessQueue[ProcessQueue 方法] --> LoopStart{循环: effectQueue 非空?};
        LoopStart -- 否 --> PostProcess[队列处理完毕];
        LoopStart -- 是 --> CheckGameOver{游戏是否结束?};
        CheckGameOver -- 是 --> NotifyUIEnd[通知 UI 结束];
        CheckGameOver -- 否 --> DequeueEffect[取出最高优先级 Effect];
        DequeueEffect --> CheckConditionPhase{是 After 阶段?};
        CheckConditionPhase -- 是 --> CheckCondition(调用 effect.Condition());
        CheckConditionPhase -- 否 --> ApplyEffect(调用 effect.Apply());
        CheckCondition -- 条件满足 --> ApplyEffect;
        CheckCondition -- 条件不满足 --> LoopStart; # 处理下一个

        ApplyEffect -- 内部调用 --> EffectLogic[Effect 内部逻辑: ResolveTarget, UpdateParams, AbilityHandler];
        EffectLogic -- 修改 --> BattleState[(BattleState)];
        EffectLogic -- 可能触发 --> TriggerCheck; # 效果连锁

        ApplyEffect --> LoopStart; # 处理下一个

        PostProcess --> NotifyUI[通知 BattleManager 更新 UI];
        NotifyUI --> CheckPlayerActionQueue{playerActionQueue 非空?};
        CheckPlayerActionQueue -- 是 --> DequeuePlayerAction[取出动作];
        DequeuePlayerAction --> PA; # 递归调用 PlayerAction 处理暂存动作
        CheckPlayerActionQueue -- 否 --> EndProcess[结束本次 ProcessQueue 调用];

    end

    Input --> BattleControl;

    style BattleState fill:#lightgrey,stroke:#333,stroke-width:2px
```

**流程图说明:**

1.  **输入来源:** 动作可以来自本地玩家 UI 或网络，事件触发则来自游戏内部逻辑。
2.  **动作接收与并发处理:** `PlayerAction` 方法首先检查效果队列是否正在处理。如果是，则将动作暂存到 `playerActionQueue`。
3.  **顺序与网络同步:** 如果效果队列空闲，则处理动作。本地动作发送 RPC，远程动作检查 `instanceId`。
4.  **效果生成与入队:** 动作被封装成初始效果，或由游戏事件触发 `CheckTriggers` 生成效果实例，最终都通过 `EnqueueEffect` 加入 `effectQueue`。`EnqueueEffect` 会计算优先级。
5.  **效果处理循环 (`ProcessQueue`):**
    *   循环从 `effectQueue` 取出最高优先级效果。
    *   检查游戏结束状态。
    *   根据效果阶段 (`After` 或其他) 和条件 (`Condition`) 判断是否执行。
    *   调用 `effect.Apply()` 执行效果，`Apply` 内部包含了目标解析、参数计算、能力执行等复杂逻辑，并可能修改 `BattleState` 或触发新的效果入队。
    *   循环直到队列为空。
6.  **后续处理:** 效果队列处理完毕后，通知 UI 更新，然后检查并处理 `playerActionQueue` 中的暂存动作。

这个流程展示了 [`Scripts/Game/Battle/Battle.cs`](./Scripts/Game/Battle/Battle.cs:0) 如何作为一个健壮的控制器，通过队列和优先级机制，有序地处理玩家输入和复杂的效果连锁，同时兼顾了网络同步的需求。