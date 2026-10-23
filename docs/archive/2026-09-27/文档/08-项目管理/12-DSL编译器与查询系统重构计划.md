# DSL 编译器与查询系统重构计划

**版本:** 1.2
**日期:** 2025-07-30
**作者:** Kilo Code

## 1. 背景与问题陈述

在对 `CardQueryExecutor` 的实现进行审查时，我们发现其内部存在一套独立的、基于反射的DSL路径解析逻辑，用于处理 `Filter`, `Sort` 等操作。这套逻辑与系统核心的 `DslEvaluator` (解释器) 并行存在，导致了以下问题：

*   **架构不一致**: 系统中存在两套功能重叠但实现不同的DSL解析引擎，增加了维护成本和认知负担。
*   **性能瓶颈**: `CardQueryExecutor` 内部的解析逻辑在处理集合筛选等高频操作时，会反复进行反射调用，造成了严重的性能瓶颈。
*   **功能受限**: 独立的解析逻辑远不如核心的 `DslEvaluator` 强大，不支持函数调用、复杂运算等，限制了查询系统的能力。

进一步调查发现，卡牌能力的**条件评估 (`Condition`)** 系统也存在同样的问题。条件检查是一个极高频的操作，当前的解释器模式同样构成了性能热点。

## 2. 重构目标

本次重构的核心目标是解决上述问题，实现一个统一、高效的DSL处理引擎。

1.  **统一架构**: 废除 `CardQueryExecutor` 中冗余的解析代码，将所有高频的DSL评估（查询过滤、条件判断等）统一到新的编译器服务上。
2.  **性能飞跃**: 引入基于C#表达式树的“编译器模式”，将DSL字符串在加载时一次性编译成高性能的.NET委托，预期可将相关操作的执行效率提升10-100倍。
3.  **提升能力**: 使 `CardQuery` 和 `Condition` 系统能够利用完整的DSL语法，包括函数调用和复杂表达式。
4.  **查询定义DSL化**: 将结构化的 `CardQuery` 对象定义，升级为由一段完整的DSL查询字符串驱动，实现查询的完全配置化。

## 3. 核心架构：编译器模式

我们将引入一个新的核心服务 `IDslCompiler`，它将与现有的 `DslEvaluator` 并行存在，分别处理不同的业务场景。

*   **DslEvaluator (解释器)**: 继续用于**一次性**的求值场景，如计算效果的`Outputs`。
*   **DslCompiler (编译器)**: 用于**重复性、高频**的求值场景，如 `CardQuery` 的过滤器和 `Condition` 的条件判断。

### 3.1. 核心组件

1.  **`IDslCompiler.cs` (接口)**: 定义编译服务的契约，提供将DSL字符串编译为强类型委托的方法。
    ```csharp
    public interface IDslCompiler
    {
        /// <summary>
        /// 将DSL表达式编译成一个返回布尔值的委托（谓词）。
        /// </summary>
        /// <typeparam name="TItem">委托的第一个参数类型，通常是'self'所代表的对象。</typeparam>
        /// <param name="dslExpression">要编译的DSL字符串。</param>
        /// <returns>一个签名为 Func<TItem, GameContext, bool> 的可复用委托。</returns>
        Func<TItem, GameContext, bool> CompilePredicate<TItem>(string dslExpression);

        /// <summary>
        /// 将DSL表达式编译成一个返回特定类型值的委托。
        /// </summary>
        /// <typeparam name="TItem">委托的第一个参数类型，通常是'self'所代表的对象。</typeparam>
        /// <param name="dslExpression">要编译的DSL字符串。</param>
        /// <returns>一个签名为 Func<TItem, GameContext, object> 的可复用委托，其返回值需要调用方进行类型转换。</returns>
        Func<TItem, GameContext, object> CompileGetter<TItem>(string dslExpression);
    }
    ```

2.  **`DslCompiler.cs` (实现)**:
    *   实现 `IDslCompiler` 接口。
    *   内置一个基于 `ConcurrentDictionary<string, Delegate>` 的线程安全缓存，以存储编译结果。
    *   依赖 `DslExpressionParser` 将DSL字符串转换为抽象语法树 (AST)。
    *   调用 `ExpressionTreeBuilder` 完成核心编译工作。

3.  **`ExpressionTreeBuilder.cs` (核心)**:
    *   负责将AST递归地转换为等效的C#表达式树 (`System.Linq.Expressions.Expression`)。
    *   能够处理 `(TItem, GameContext)` 这种多参数上下文，正确地将DSL中的 `self` 映射到 `TItem` 参数，将 `player`, `opponent` 等全局关键字映射到 `GameContext` 的方法调用或属性上。

### 3.2. 架构图

```mermaid
graph TD
    subgraph DSL Processing
        A[DSL String] --> B{Lexer & Parser};
        B --> C[AST];
    end

    subgraph Execution Engines
        C --> D[DslEvaluator (Interpreter)];
        D --> E[Output: Immediate Value];

        C --> F[DslCompiler (Compiler)];
        F --> G[Output: Cached, Reusable Func<>];
    end

    subgraph Consumers
        H[EffectExecutor] --> D;
        I[CardQueryExecutor] --> F;
        J[Condition Evaluator] --> F;
    end

    style F fill:#ccf,stroke:#333,stroke-width:2px
    style G fill:#cfc,stroke:#333,stroke-width:2px
```

## 4. 实施计划：编译器核心 (已完成)

*   **Phase 1: 核心编译器设计**
    *   [x] 1.1 设计 `IDslCompiler` 接口，支持编译生成不同签名的委托。
    *   [x] 1.2 优化设计，将 `GameContext` 和 `TItem` 作为多参数直接传入委托。
    *   [x] 1.3 设计并实现编译结果的缓存服务。

*   **Phase 2: `DslCompiler` 核心实现**
    *   [x] 2.1 创建 `ExpressionTreeBuilder`，将DSL的AST转换为表达式树。
    *   [x] 2.2 在 `ExpressionTreeBuilder` 中实现对 `(TItem, GameContext)` 多参数上下文的支持。
    *   [x] 2.3 实现将表达式树编译为强类型委托的核心逻辑。

*   **Phase 3: 应用与重构 (双轨并行)**
    *   [x] 3.1 **[Query]** 重构 `CardQueryExecutor`，注入 `IDslCompiler` 并用编译后的委托替换旧的筛选逻辑。
    *   [x] 3.2 **[Condition]** 重构 `Condition` 运行时类，在构造时编译DSL并存储委托。
    *   [x] 3.3 **[Condition]** 修改 `Condition.Evaluate` 方法，使其直接调用编译好的委托。

*   **Phase 4: 验证与收尾**
    *   [x] 4.1 为 `DslCompiler` 编写全面的单元测试。
    *   [x] 4.2 为重构后的 `CardQueryExecutor` 和 `Condition` 评估编写集成测试。
    *   [x] 4.3 更新所有相关的架构和系统设计文档。

## 5. 实施计划：查询语法 (Query Syntax) DSL 集成

在完成了编译器核心的构建后，下一步是利用这个强大的工具，将查询的**定义**过程也完全 DSL 化。这个阶段的核心是**新增**一个 `CardQueryDslParser`，将人类可读的 DSL 查询字符串转换为现有的、基于操作管道的 `CardQueryConfig` 对象，而**非修改**已经稳定运行的 `CardQueryExecutor` 核心逻辑。

### 5.1. 目标

当前 `CardQueryConfig` 是一个通过 C# 代码构建的、基于操作列表的配置对象。我们的目标是设计一套完整的查询 DSL，使得我们可以用一段简单的字符串（例如 `player.hand.query(self.cost > 3)`) 来生成这个 `CardQueryConfig` 对象，实现查询的完全配置化，同时完全复用现有的 `CardQueryExecutor` 执行引擎。

### 5.2. DSL 语法设计 (最终方案)

我们采用与现有 DSL 风格统一的**链式方法调用**风格。

**基本结构**:
`<source_expression>.query([filter_expression]).orderBy([sort_expression] [asc|desc], ...).take([count])`

**示例**:
*   `player.hand.query(self.cost > 3)`: 查找玩家手中所有消耗大于3的卡牌。
*   `opponent.board.query().orderBy(self.attack desc, self.cost asc).take(1)`: 查找敌方场上攻击力最高（如果攻击力相同则按消耗从低到高）的1个随从。

**静态数据源访问**:
为了支持对游戏静态数据库（如所有卡牌定义）的查询，我们引入一个特殊的顶级访问符 `db`。

*   `db.cards.query(self.type == "Follower" and self.cost >= 8)`: 查找数据库中所有消耗大于等于8的随从牌。

当查询源为 `db.cards` 时，`self` 关键字将自动指向 `CardDefinition` 类型；当查询源为 `player.hand` 等动态集合时，`self` 将指向 `CardInstance` 类型。

### 5.3. 架构设计 (最终方案)

我们将新增一个解析器 `CardQueryDslParser`，它作为 `CardQueryExecutor` 的“前端”，负责将 DSL 字符串翻译成 `CardQueryExecutor` 所理解的 `CardQueryConfig` 对象。

**数据流图 (经验证)**:
```mermaid
graph TD
    subgraph "1. DSL 解析阶段 (新)"
        A[DSL: "player.hand.query(...)"] --> B{CardQueryDslParser};
        B --> C["CardQueryConfig 对象<br>SourcePoolExpression: 'player.hand'<br>Operations: [FilterOperation, SortOperation, ...]"];
    end

    subgraph "2. 查询执行阶段 (现有)"
        C --> D{CardQueryExecutor};
        D -- "ExecuteQuery(config, context)" --> E{遍历 Operations};
        E -- "op is FilterOperation" --> F[ApplyFilterOperation<br>使用 DslCompiler];
        E -- "op is SortOperation" --> G[ApplySortOperation<br>使用 DslCompiler];
        E -- "..." --> H[...];
        H --> I[最终结果];
    end
```

### 5.4. 详细实施计划 (已根据现有代码验证和修正)

*   **Phase 5.1: 创建 `CardQueryDslParser`**
    *   [x] **任务 5.1.1**: 定义 `ICardQueryDslParser` 接口及其 `Parse(string dsl)` 方法，返回 `CardQueryConfig`。
    *   [x] **任务 5.1.2**: 创建 `CardQueryDslParser` 实现类。
    *   [x] **任务 5.1.3**: 实现 `Parse` 方法的核心逻辑。
        *   **核心思路**: 使用正则表达式，按链式调用的自然顺序，依次分割出源表达式 (`source_expression`) 和各个操作 (`.query()`, `.orderBy()`, `.take()` 等)。
        *   **步骤**:
            1.  从DSL字符串的开头提取出源表达式 (e.g., `player.hand`)。
            2.  使用正则表达式匹配后续的每一个方法调用块 (e.g., `.query(...)`, `.orderBy(...)`)。
            3.  遍历匹配到的方法调用块，根据方法名 (`query`, `orderBy`, `take`) 创建对应的 `FilterOperation`, `SortOperation`, `SelectionOperation` 对象。
            4.  将解析出的操作对象按其在DSL字符串中出现的顺序添加到 `CardQueryConfig.Operations` 列表中。
            5.  根据 `SourcePoolExpression` 是否为 `db.cards` 来设置 `TargetType`。
    *   [x] **任务 5.1.4**: 为 `CardQueryDslParser` 编写全面的单元测试，覆盖简单查询、完整查询、静态数据源查询以及各种边界情况。

*   **Phase 5.2: 集成与重构**
    *   [x] **任务 5.2.1**: 在 `CardQueryExecutor` 中注入 `ICardQueryDslParser`。
    *   [x] **任务 5.2.2**: 在 `ICardQueryExecutor` 接口和 `CardQueryExecutor` 类中，创建一个新的主入口方法 `ExecuteQuery(string dsl, GameContext context)`。
    *   [x] **任务 5.2.3**: 在新的 `ExecuteQuery(dsl)` 方法中，调用 `_cardQueryDslParser.Parse(dsl)` 将字符串转换为 `CardQueryConfig` 对象。
    *   [x] **任务 5.2.4**: 调用现有的 `ExecuteQuery(CardQueryConfig config, GameContext context)` 方法，传入上一步生成的 `config` 对象来执行查询。
    *   [x] **任务 5.2.5**: 决策保留 `ExecuteQuery(config)` 为 `public`，以支持未来可能的、以编程方式构建查询的需求。

*   **Phase 5.3: 端到端验证**
    *   [x] **任务 5.3.1**: 创建 `CardQueryDslIntegrationTests.cs` 测试文件。
    *   [x] **任务 5.3.2**: 编写测试，直接调用 `ICardQueryExecutor.ExecuteQuery(dsl, context)`，并传入完整的 DSL 字符串。
    *   [x] **任务 5.3.3**: 测试用例已覆盖：
        *   仅有 `source` 和 `query` 的简单查询。
        *   包含 `orderBy` 和 `take` 的完整查询。
        *   对静态数据源 `db.cards` 的查询。
        *   对动态游戏状态 `player.hand` 的查询。
    *   [x] **任务 5.3.4 (追加)**: 修复了集成测试中暴露的 `GameContext` 依赖注入问题，并解决了由此导致的 `CardQueryExecutorTests` 中的“幽灵”测试不稳定 bug。

*   **Phase 5.4 (追加): 技术债务清理**
    *   [x] **任务 5.4.1**: 解决了项目中与可空引用类型相关的编译警告，提升了代码健壮性。

## 6. 预期收益

1.  **性能大幅提升**: 高频操作（查询、条件判断）的执行效率预计提升10-100倍。
2.  **代码质量提高**: 移除冗余、低效的解析代码，使 `CardQueryExecutor` 等类更加简洁、健壮。
3.  **架构一致性**: 建立统一的、可扩展的DSL处理引擎，为未来更复杂的游戏逻辑奠定坚实基础。
4.  **错误提前发现**: DSL的语法错误将在编译阶段（游戏加载时）被捕获，而不是在运行时才暴露。
5.  **设计灵活性**: 查询逻辑可以完全由外部字符串定义，极大地提升了卡牌效果设计的灵活性和动态性。

## 7. 实现过程中的关键决策与挑战

在本次重构的集成与验证阶段，我们遇到了一个由静态依赖引起的严重问题，导致 `CardQueryExecutorTests.cs` 中的单元测试变得不稳定（“幽灵测试”）。

*   **问题现象**: 测试在完整运行时（`dotnet test`）能够通过，但在单独运行时则会因 `GameContext has not been set` 异常而失败。
*   **根本原因**:
    1.  **状态污染**: 静态单例 `GameDataProvider.Instance` 在测试之间没有被重置，导致状态泄露。
    2.  **初始化失败**: 静态服务定位器 `GameServiceProvider.ContextAccessor` 从未在 `CardQueryExecutorTests` 测试类中被正确初始化。它能“偶然”通过，是因为其他测试类（如 `CardQueryDslIntegrationTests`）先运行并初始化了这个静态属性。
*   **解决方案**:
    1.  **正确初始化**: 借鉴 `CardQueryDslIntegrationTests` 的模式，在 `CardQueryExecutorTests` 的构造函数中，使用其自身的依赖注入容器来正确初始化 `GameServiceProvider.ContextAccessor`。
    2.  **测试隔离**: 为 `CardQueryExecutorTests` 中的每个测试方法添加 `try...finally` 块，确保在测试结束后，无论成功或失败，`GameDataProvider.Instance` 都会被重置到一个干净的状态。

这个问题的解决过程凸显了在测试中处理静态依赖的挑战，并巩固了测试隔离作为核心原则的重要性。