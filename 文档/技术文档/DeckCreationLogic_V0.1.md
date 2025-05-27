# 牌库构建逻辑 V0.1 (Deck Creation Logic V0.1)

## 1. 目标

修改游戏初始化流程，使得玩家的初始牌库能够包含数据库中定义的多种卡牌，而不是仅限于单一的 "F001" (哥布林)。

## 2. 设计思路

核心思路是在游戏状态创建时 (`GameService.GetOrCreateGameStateAsync`)，从数据库加载所有可用的卡牌定义，然后基于这些定义构建一个包含多种卡牌、经过洗牌的牌库。

## 3. 实现步骤

1.  **扩展 `ICardRepository`**:
    - 在 `ICardRepository` 接口 ([`Server/Domain/Interfaces/ICardRepository.cs`](Server/Domain/Interfaces/ICardRepository.cs)) 中添加新方法签名：
      ```csharp
      Task<List<CardDefinition>> GetAllCardDefinitionsAsync();
      ```
2.  **实现 `EFCardRepository`**:
    - 在 `EFCardRepository` 类 ([`Server/Infrastructure/Data/Repositories/EFCardRepository.cs`](Server/Infrastructure/Data/Repositories/EFCardRepository.cs)) 中实现 `GetAllCardDefinitionsAsync` 方法。该实现将使用 Entity Framework Core 从 `CardDefinitions` 数据表中检索所有记录。
      ```csharp
      public async Task<List<CardDefinition>> GetAllCardDefinitionsAsync()
      {
          return await _context.CardDefinitions.ToListAsync();
      }
      ```
3.  **修改 `GameService.GetOrCreateGameStateAsync`**:
    - 移除硬编码获取 "F001" 的逻辑。
    - 在方法开始处（或适当位置，确保在构建牌库前），调用 `await cardRepository.GetAllCardDefinitionsAsync()` (在正确的 `IServiceScope` 内) 来获取所有卡牌定义列表 `allCardDefinitions`。
    - 如果 `allCardDefinitions` 为空或 null，则记录错误并中止游戏创建。
    - 移除对旧 `CreateDeckFromDefinitionAsync` 的调用。
    - 调用新的内部牌库构建逻辑（如下所述）来生成 `player1Deck` 和 `player2Deck`。
4.  **实现内部牌库构建逻辑 (`BuildPlayerDeckInternal`)**:
    - 此逻辑可以在 `GameService.cs` 内作为一个私有辅助方法实现，或者如果将来预期更复杂，可以考虑提取到单独的类中（目前作为私有方法即可）。
    - **输入**: `List<CardDefinition> allDefinitions`, `int deckSize`
    - **输出**: `List<CardDto>` (代表玩家的牌库)
    - **流程**:
      - 创建一个空的 `List<CardDefinition>` 作为“卡池 (Master List)”。
      - 遍历输入的 `allDefinitions` 列表。
      - 对于列表中的每个 `cardDefinition`，向卡池中添加 3 个该定义的副本。（注意：这是简化逻辑，未来可能需要根据卡牌稀有度或限制进行调整）。
      - 调用 `ShuffleDeck(masterList)` (使用已有的 Fisher-Yates 实现) 来洗牌卡池。
      - 创建一个空的 `List<CardDto>` 作为结果 `playerDeck`。
      - 从洗好的 `masterList` 顶部抽取 `deckSize` 张 `CardDefinition`。
      - 对于抽取的每一张 `CardDefinition`：
        - 创建一个新的 `CardDto` 实例。
        - 为其分配一个唯一的 `Uid` (`Guid.NewGuid().ToString()`)。
        - 从 `CardDefinition` 复制基础属性 (`CardId`, `Name`, `Cost`, `Type`, `BaseAttack`, `BaseHealth` 等) 到 `CardDto`。
        - 将创建的 `CardDto` 添加到 `playerDeck`。
      - 返回 `playerDeck`。

## 4. 流程可视化 (Mermaid)

```mermaid
flowchart TD
    subgraph GetOrCreateGameStateAsync
        A[开始创建游戏状态] --> B{调用 ICardRepository.GetAllCardDefinitionsAsync};
        B --> C[获取所有卡牌定义列表: AllDefs];
        C -- AllDefs, DeckSize --> D(调用 BuildPlayerDeckInternal for P1);
        D --> E[生成 P1 牌库: P1Deck];
        C -- AllDefs, DeckSize --> F(调用 BuildPlayerDeckInternal for P2);
        F --> G[生成 P2 牌库: P2Deck];
        E & G --> H{继续创建 PlayerStateDto ...};
    end

    subgraph BuildPlayerDeckInternal
        I[输入: AllDefs, DeckSize] --> J{创建空卡池: MasterList};
        J --> K{遍历 AllDefs};
        K -- 为每个 CardDef --> L{添加 3 份到 MasterList};
        L --> M[卡池构建完成];
        M --> N{调用 ShuffleDeck(MasterList)};
        N --> O{从 MasterList 顶部取 DeckSize 张};
        O --> P{为每张卡创建 CardDto (带唯一 UID)};
        P --> Q[输出: PlayerDeck (List<CardDto>)];
    end

    style GetOrCreateGameStateAsync fill:#f9f,stroke:#333,stroke-width:2px
    style BuildPlayerDeckInternal fill:#ccf,stroke:#333,stroke-width:2px
```

## 5. 假设与待办

- **牌库大小**: 固定为 40。
- **卡牌副本限制**: 暂时忽略，所有非传说卡牌（如果未来有此概念）都按 3 份加入卡池。
- **待办**:
  - 未来可能需要更复杂的牌库构建规则（例如，职业限制、稀有度分布、禁止卡牌等）。
  - `CardDefinition` 实体可能需要添加字段来支持这些高级规则（如 `MaxCopiesInDeck`, `Rarity`, `ClassRestriction`）。
