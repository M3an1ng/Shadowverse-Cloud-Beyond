# Unity 项目 C# 脚本架构与逻辑文档 - 编写计划 V1.0

**1. 文档目标**

- 清晰地阐述该 Unity 项目中 C# 脚本的整体架构和设计模式。
- 详细说明核心系统、游戏逻辑、网络和 UI 相关脚本的主要职责和工作流程 **(根据要求，将进行深入解析)**。
- 解释 C# 脚本如何加载、解析和使用 `StreamingAssets` 目录下的数据文件 (CSV/XML)，体现数据驱动设计的思想。
- 为初学者提供一个理解项目代码结构和关键逻辑的指南。

**2. 文档结构大纲**

- **2.1 顶层模块划分** (System, Game, Network, UI 等)
- **2.2 架构图** (使用 Mermaid `graph TD` 可视化模块交互)

  ```mermaid
  graph TD
      A[System Core] --> B(Game Logic);
      A --> C(Network);
      A --> D(UI);
      B --> A;
      B --> C;
      B --> D;
      C --> A;
      C --> B;
      D --> B;
      D --> A;

      subgraph System Core
          direction LR
          Sys1[ResourceManager]
          Sys2[DatabaseManager]
          Sys3[...]
      end

      subgraph Game Logic
          direction LR
          Game1[Game/Battle Manager]
          Game2[Card System]
          Game3[Player/Leader]
          Game4[Effect Engine]
      end

      subgraph Network
          direction LR
          Net1[NetworkManager (Photon)]
      end

      subgraph UI
          direction LR
          UI1[UI Managers]
          UI2[Views (BattleUnitView, etc.)]
      end

      Data[(StreamingAssets: CSV/XML)] -.-> Sys1;
      Sys1 -.-> Sys2;
      Sys2 -.-> Game2;
      Sys2 -.-> Game3;
      Game4 -- Uses --> Game2;
      Game4 -- Uses --> Game3;
  ```

- **2.3 主要设计模式应用** (如 Singleton, Manager Pattern)
- **3. 核心系统 (`Scripts/System/Core`)**
  - 3.1 **`ResourceManager.cs`**
    - 职责: 资源加载，特别是 `StreamingAssets` 数据解析 (CSV, XML)。
    - 关键流程: 异步加载过程说明 **(深入解析)**。
    - 数据映射机制: 如何将文本数据转化为 C# 对象。
    - 交互: 与 `DatabaseManager` 的关系。
    - _可选图表_: 资源加载时序图 (Mermaid `sequenceDiagram`)。
  - 3.2 **`DatabaseManager.cs`**
    - 职责: 统一管理和提供对已加载游戏数据的访问接口。
    - 数据访问模式: 其他系统如何查询卡牌、效果等数据。
- **4. 游戏逻辑 (`Scripts/Game`)**
  - 4.1 **`GameManager.cs` / `BattleManager.cs`**
    - 职责: 控制游戏主循环、回合流程、战斗逻辑、胜负判断。
    - 核心流程: 回合阶段划分与转换逻辑 **(深入解析)**。
    - 状态管理: 游戏状态机说明。
    - _可选图表_: 游戏状态/回合流程图 (Mermaid `stateDiagram`)。
  - 4.2 **卡牌系统 (`Scripts/Game/Card`)**
    - `Card.cs` / `CardData.cs`: 卡牌实例与静态数据的表示。
    - `CardDatabase.cs`: 枚举定义 (Type, Trait, Keyword) 及其作为数据-代码桥梁的重要性。
    - `CardFilter.cs`: 效果目标选择逻辑解析，如何实现 `effect.csv` 中的 `target` 语法 **(深入解析)**。
  - 4.3 **效果执行引擎** (需定位具体实现脚本)
    - 职责: 解析 `effect.csv`，执行卡牌效果 (Ability + Option)。
    - 关键流程: 效果触发、条件检查、目标选取、能力执行 **(深入解析)**。
    - 与游戏状态交互: 如何修改场面、玩家属性等。
    - _可选图表_: 典型效果执行时序图 (Mermaid `sequenceDiagram`)。
  - 4.4 **玩家/主战者系统** (需定位具体实现脚本)
    - 职责: 管理玩家状态（生命、PP、EP、手牌、牌库、墓地）。
- **5. 网络系统 (`Scripts/System/Network`)**
  - 5.1 **`NetworkManager.cs`**
    - 职责: 基于 Photon PUN 实现网络同步。
    - 核心功能: 连接管理、房间同步、游戏事件 RPC 调用（保持高层级描述，除非要求深入）。
- **6. UI 系统 (`Scripts/UI`)**
  - 6.1 **UI 管理器与视图** (如 `BattleUnitView.cs`)
    - 职责: 游戏数据显示、用户输入处理。
    - 交互模式: UI 如何获取游戏数据并发送玩家操作指令（保持高层级描述）。
- **7. 数据与代码交互总结**
  - 7.1 再次强调数据文件 (`info.csv`, `effect.csv`, `version.xml`) 的作用。
  - 7.2 总结 C# 脚本如何依赖和利用这些数据来驱动游戏逻辑。
- **8. 附录**
  - 8.1 关键枚举定义列表 (从 `CardDatabase.cs` 提取)。
  - 8.2 `effect.csv` 关键列（timing, target, ability, option）语法简要说明。
