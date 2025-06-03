# BUG-017 AI 攻击失败 - 调查计划 V1

## 1. 目标 (Goal)

诊断并修复 AI 玩家在执行其回合时，无法按预期执行攻击动作的问题 (BUG-017)。

## 2. 当前状态 (Current Status)

- **日志分析:** [`Server/Logs/localhost-1748869907575.log`](./Server/Logs/localhost-1748869907575.log) 显示 AI 能够正常出牌和进化（如果适用），但在执行完这些动作后，回合直接结束，没有记录到任何尝试攻击或攻击失败的日志。
- **代码分析:**
    - [`Server/Services/AiTurnExecutor.cs`](./Server/Services/AiTurnExecutor.cs): AI 回合的主循环逻辑位于 `ExecuteTurnAsync`。它调用 `IAiService.DecideAttacksAsync` 来获取攻击列表，然后遍历列表并调用 `IGameService.AttackAsync` 来执行攻击。对 `GameLogicException` 会记录并跳过，对其他 `Exception` 会中止回合。
    - [`Server/Services/GameService.cs`](./Server/Services/GameService.cs): `AttackAsync` 方法包含详细的攻击前验证逻辑（回合、阶段、攻击者状态、目标、守护等）。

## 3. 可能原因推断 (Hypotheses)

根据现有信息，最可能导致 AI 不攻击的原因排序如下：

1.  **AI 决策问题:** `IAiService.DecideAttacksAsync` 未能正确识别有效的攻击者和目标，返回了 `null` 或空列表。这导致 `AiTurnExecutor` 跳过了攻击执行环节。
2.  **执行验证失败 (GameLogicException):** `GameService.AttackAsync` 在 AI 尝试的每次攻击时，都因为某个验证条件不满足而抛出 `GameLogicException`。`AiTurnExecutor` 捕获此异常后跳过攻击，最终因没有成功执行任何动作 (`actionTaken == false`) 而结束回合。
3.  **执行意外失败 (Exception):** `GameService.AttackAsync` 在 AI 尝试第一次攻击时就抛出了非 `GameLogicException` 的其他异常。这导致 `AiTurnExecutor` 捕获异常并立即中止整个 AI 回合。

## 4. 调查计划 (Investigation Plan)

由于无法直接访问 `IAiService` 的代码，首要步骤是通过增强 `AiTurnExecutor.cs` 的日志输出来定位问题环节：

1.  **记录 AI 决策:** 在调用 `_aiService.DecideAttacksAsync` ([`Server/Services/AiTurnExecutor.cs:194`](./Server/Services/AiTurnExecutor.cs:194)) 之后，立即添加日志，详细记录返回的 `attackActions` 列表内容。
    *   **日志内容:** 攻击动作的数量；如果非空，列出每个动作的 `AttackerUid` 和 `TargetUid`。
    *   **预期判断:** 如果日志显示返回 `null` 或空列表，则问题很可能在 `IAiService`。
2.  **记录攻击尝试:** 在 `AiTurnExecutor.cs` 的攻击 `foreach` 循环内部，调用 `_gameService.AttackAsync` ([`Server/Services/AiTurnExecutor.cs:217`](./Server/Services/AiTurnExecutor.cs:217)) 之前，添加日志记录明确指出“正在尝试执行攻击”，并包含 `AttackerUid` 和 `TargetUid`。
    *   **预期判断:** 如果步骤 1 日志显示有攻击动作，但此日志从未出现，说明循环未进入或提前退出。如果此日志出现，说明 AI 确实尝试了攻击。
3.  **记录异常详情:** 在 `AiTurnExecutor.cs` 的 `catch (GameLogicException ex)` ([`Server/Services/AiTurnExecutor.cs:222`](./Server/Services/AiTurnExecutor.cs:222)) 和 `catch (Exception ex)` ([`Server/Services/AiTurnExecutor.cs:230`](./Server/Services/AiTurnExecutor.cs:230)) 块内，添加更详细的日志，记录完整的异常信息 (`ex.ToString()`) 以及发生异常时的攻击者和目标 UID。
    *   **预期判断:** 如果日志显示 `GameLogicException`，则问题在 `GameService.AttackAsync` 的验证逻辑。如果显示其他 `Exception`，则可能是 `GameService.AttackAsync` 内部的意外错误。

## 5. AI 回合执行流程图 (AI Turn Execution Flowchart)

```mermaid
graph TD
    A[Start ExecuteTurnAsync] --> B{Fetch GameState};
    B --> C{Loop Iteration Start};
    C --> D{Max Actions?};
    D -- Yes --> X[Break Loop];
    D -- No --> E{Decide Play Card};
    E --> F{Play Card Action?};
    F -- Yes --> G[Execute PlayCardAsync];
    G -- Success --> C;
    G -- GameLogicException --> H{Decide Evolve};
    G -- Exception --> X;
    F -- No --> H;
    H --> I{Evolve Action?};
    I -- Yes --> J[Execute EvolveAsync];
    J -- Success --> C;
    J -- GameLogicException --> K{Decide Attacks};
    J -- Exception --> X;
    I -- No --> K;
    K --> L[Call DecideAttacksAsync];
    L --> M{Attack Actions?};
    M -- No --> N[actionTaken = false];
    M -- Yes --> O{Loop Through Attacks};
    O --> P[Execute AttackAsync];
    P -- Success --> Q{anyAttackSucceeded = true};
    P -- GameLogicException --> R[Log & Skip Attack];
    P -- Exception --> S[actionTaken = false];
    S --> X;
    Q --> O;
    R --> O;
    O -- Loop End --> T{anyAttackSucceeded?};
    T -- Yes --> U[actionTaken = true];
    T -- No --> N;
    U --> C;
    N --> V{actionTaken?};
    V -- No --> X;
    V -- Yes --> C; % If Play/Evolve was true, continues. If Attack failed, breaks.
    X --> Y[Call EndTurnAsync];
    Y --> Z[End ExecuteTurnAsync];

    subgraph Potential Failure Points (BUG-017)
        L -- Returns Null/Empty --> M;
        P -- Throws GameLogicException --> R;
        P -- Throws Exception --> S;
    end

    style L fill:#f9f,stroke:#333,stroke-width:2px
    style P fill:#f9f,stroke:#333,stroke-width:2px
```

## 6. 后续步骤 (Next Steps)

1.  根据此计划，在 [`Server/Services/AiTurnExecutor.cs`](./Server/Services/AiTurnExecutor.cs) 中添加必要的日志代码。
2.  重新运行游戏，复现 AI 回合。
3.  分析新的日志输出，确定 BUG-017 的根本原因。
4.  根据原因制定具体的代码修复方案。