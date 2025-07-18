# Effect Handler 重构指南

## 重构状态概览

### ✅ 第一阶段 [COMPLETED] - 基类继承统一
- **目标**: 修复已使用ITargetProcessingService但未继承EffectHandlerBase的Handler类
- **完成时间**: 2024年当前重构周期
- **状态**: 全部完成

### ✅ 第二阶段 [COMPLETED] - 目标处理服务集成
- **目标**: 为已继承EffectHandlerBase但未使用ITargetProcessingService的Handler添加目标处理服务
- **完成时间**: 2024年当前重构周期
- **状态**: 全部完成

### 🔧 关键问题解决记录
- **EffectResult.cs文件扩展名问题**: 发现并修复了`Server/Domain/Abilities/Effects/EffectResult`文件缺少`.cs`扩展名的问题，这是导致`GameStateActionResult`和`IndividualActionResult`类型无法识别的根本原因
- **编译错误解决**: 从23个EffectHandler相关编译错误减少到0个
- **架构统一**: 所有14个Handler类现在都遵循统一的架构模式

## 1. 重构目标

统一 Effect Handler 的目标处理逻辑，利用 `ITargetProcessingService` 服务，旨在：

*   **提高代码复用性:** 将通用的目标查找、过滤、循环处理逻辑抽象到服务中。
*   **增强可测试性:** Effect Handler 本身逻辑更聚焦于单一效果的应用，更容易进行单元测试。`ITargetProcessingService` 也可以独立测试。
*   **提升可维护性:** 结构更清晰，修改或扩展效果处理逻辑时影响范围更小。
*   **统一错误处理和事件聚合:** 由 `ITargetProcessingService` 统一处理多个目标的执行结果和事件聚合。

## 2. 核心设计思路

1.  **引入 `ITargetProcessingService`:** 这是重构的核心。该服务负责接收游戏上下文、目标类型、以及一个处理**单个目标**的委托（Action）。
2.  **封装核心逻辑到 Action 委托:** 将原本在 `EffectHandler.ApplyAsync` 中针对单个目标实例（如 `CardInstance` 或 `PlayerState`）执行的所有操作（包括：获取相关状态、进行资格检查、修改状态、生成领域事件）封装到一个 `Func<TTarget, GameContext, Task<IndividualActionResult>>` 类型的委托中。
    *   `TTarget`: 目标类型，如 `CardInstance` 或 `PlayerState`。
    *   `GameContext`: 传递游戏上下文。
    *   `IndividualActionResult`: Action 委托的返回值，包含对单个目标操作的结果（成功/失败、消息、生成的事件列表）。
3.  **`EffectHandler.ApplyAsync` 的职责:**
    *   准备好上述的 Action 委托。
    *   调用 `_targetProcessingService.ProcessTargetsAsync` 方法，传入上下文、目标类型、Action 委托和效果名称（用于日志）。
4.  **领域事件驱动:** 确保在 Action 委托内部生成正确的、定义在**领域层**（`Domain/Events`）的事件。如果事件缺失，应先在领域层定义。
5.  **依赖注入:** 确保 `EffectHandler` 和 `ITargetProcessingService` 所需的依赖（如 `ICardRepository`, `ILogger`, `IMapper` 等）都通过构造函数正确注入。

## 3. 重构步骤详解

### 3.1 迁移到 `EffectHandlerBase<T>` (以 `DamageEffectHandler` 为例)

对于之前直接实现 `IEffectHandler<TParams>` 的处理器，需要进行以下调整以继承 `EffectHandlerBase<TParams>`：

1.  **修改类声明:** 将 `public class MyHandler : IEffectHandler<MyParams>` 修改为 `public class MyHandler : EffectHandlerBase<MyParams>`。
2.  **添加 `using`:** 确保添加了 `using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;`。
3.  **调整构造函数:**
    *   构造函数参数应接受 `ILogger` (非泛型) 而不是 `ILogger<MyHandler>`。
    *   调用基类构造函数：在构造函数声明后添加 `: base(logger)`。
    *   移除类内部私有的 `_logger` 字段，后续直接使用基类提供的受保护成员 `Logger`。
    *   示例 (`DamageEffectHandler`):
        ```csharp
        public DamageEffectHandler(ILogger logger, ITargetProcessingService targetProcessingService)
            : base(logger) // Call base constructor with ILogger
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }
        ```
4.  **添加 `override`:** 在 `ApplyAsync` 方法声明前添加 `override` 关键字。
5.  **使用基类 Logger:** 将代码中所有对私有 `_logger` 的引用改为直接使用 `Logger`。

### 3.2 使用 `ITargetProcessingService` (以 `EvolveEffectHandler` 为例)

以下是利用 `ITargetProcessingService` 重构的核心步骤：

1.  **依赖注入:**
    *   在 Handler 的构造函数中注入 `ITargetProcessingService`。
    *   如果 Action 委托内部需要映射 DTO，则注入 `AutoMapper.IMapper`。

2.  **定义 Action 委托:**
    *   在 `ApplyAsync` 方法内部，定义一个 `Func<TTarget, GameContext, Task<IndividualActionResult>> myAction` 委托变量 (例如 `evolveAction` 或 `damageAction`)。`TTarget` 是效果要处理的目标类型（如 `CardInstance`）。

3.  **迁移核心逻辑到 Action 委托:**
    *   将原 `ApplyAsync` 方法中**所有**针对单个目标实例 (`targetInstance`) 的处理逻辑完整地移动到 `myAction` 委托的 `{}` 代码块内。这包括：
        *   获取相关状态（如 `ownerPlayer`, `baseDefinition`）。
        *   执行所有资格检查。
        *   执行实际的效果操作（如修改属性、调用方法）。
        *   **注意:** 确保委托内部使用传递的 `targetInstance` 和 `ctx` 参数。
    *   **重要:** Action 委托必须返回 `IndividualActionResult`。

4.  **处理领域事件:**
    *   在 `myAction` 内部，确定并创建需要生成的领域事件。
    *   确保事件类已在领域层定义。
    *   将创建的事件实例添加到一个临时的 `List<IGameEvent>` 中。
    *   在 `myAction` 返回的 `IndividualActionResult` 中，设置 `Success = true`，并将其 `GeneratedEvents` 属性设置为包含已生成事件的列表。
    *   示例 (`DamageEffectHandler` 的 `damageAction`):
        ```csharp
        var generatedEvents = card.TakeDamage(parameters.DamageAmount, ctx.SourceCard);
        return new IndividualActionResult
        {
            TargetId = TargetId.Card(card.InstanceId),
            Success = true,
            Message = $"Applied {parameters.DamageAmount} damage.",
            GeneratedEvents = generatedEvents // Include the generated events
        };
        ```

5.  **调用处理服务:**
    *   在 `ApplyAsync` 方法的末尾，调用 `_targetProcessingService.ProcessTargetsAsync`。
    *   传入参数：`context`, 目标类型 (`TargetIdType.CardInstance` 或 `TargetIdType.Player`), 另一个目标类型的 Action (如果需要，否则为 `null`), `myAction`, 效果名称字符串。
    *   `ApplyAsync` 方法直接返回 `ProcessTargetsAsync` 的结果 (`GameStateActionResult`)。

6.  **清理:**
    *   删除 `ApplyAsync` 方法中所有已被迁移到 `myAction` 委托内的重复代码块。
    *   移除不再需要的 `using` 指令或局部变量。

7.  **编译与测试:**
    *   解决所有编译错误。
    *   运行测试或手动测试，确保逻辑正确且事件触发符合预期。

## 4. 注意事项

*   **Action 委托的独立性:** 确保 Action 委托内部的逻辑尽可能独立，只依赖传入的目标和上下文。
*   **错误处理:** Action 委托内部应妥善处理可能发生的错误，并通过返回 `Success = false` 和相应的 `Message` 来通知 `ITargetProcessingService`。
*   **事件聚合:** `ITargetProcessingService` 会自动收集所有成功的 `IndividualActionResult` 中的 `GeneratedEvents`。
*   **日志记录:** 在 Action 委托内部和 `EffectHandler` 本身添加适当的日志记录。

## 5. Handler类重构状态详表

### 第一阶段重构 [COMPLETED]
修复已使用ITargetProcessingService但未继承EffectHandlerBase的Handler类：

| Handler类 | 重构前状态 | 重构后状态 | 完成状态 |
|-----------|------------|------------|----------|
| HealEffectHandler | 使用ITargetProcessingService，直接实现IEffectHandler | 继承EffectHandlerBase + ITargetProcessingService | ✅ [COMPLETED] |
| ModifyStatsEffectHandler | 使用ITargetProcessingService，直接实现IEffectHandler | 继承EffectHandlerBase + ITargetProcessingService | ✅ [COMPLETED] |
| SummonUnitEffectHandler | 使用ITargetProcessingService，直接实现IEffectHandler | 继承EffectHandlerBase + ITargetProcessingService | ✅ [COMPLETED] |
| AddKeywordEffectHandler | 直接实现IEffectHandler，手动目标处理 | 继承EffectHandlerBase + ITargetProcessingService | ✅ [COMPLETED] |
| DestroyUnitEffectHandler | 直接实现IEffectHandler，手动目标处理 | 继承EffectHandlerBase + ITargetProcessingService | ✅ [COMPLETED] |
| DrawCardEffectHandler | 直接实现IEffectHandler，手动目标处理 | 继承EffectHandlerBase + ITargetProcessingService | ✅ [COMPLETED] |
| RemoveKeywordEffectHandler | 直接实现IEffectHandler，手动目标处理 | 继承EffectHandlerBase + ITargetProcessingService | ✅ [COMPLETED] |

### 第二阶段重构 [COMPLETED]
为已继承EffectHandlerBase但未使用ITargetProcessingService的Handler添加目标处理服务：

| Handler类 | 重构前状态 | 重构后状态 | 完成状态 |
|-----------|------------|------------|----------|
| AddCardToHandEffectHandler | 继承EffectHandlerBase，手动目标处理 | 继承EffectHandlerBase + ITargetProcessingService | ✅ [COMPLETED] |
| ModifyPPEffectHandler | 继承EffectHandlerBase，手动目标处理 | 继承EffectHandlerBase + ITargetProcessingService | ✅ [COMPLETED] |
| ModifyEPEffectHandler | 继承EffectHandlerBase，手动目标处理 | 继承EffectHandlerBase + ITargetProcessingService | ✅ [COMPLETED] |
| ModifyPPMaxEffectHandler | 继承EffectHandlerBase，手动目标处理 | 继承EffectHandlerBase + ITargetProcessingService | ✅ [COMPLETED] |
| TransformEffectHandler | 继承EffectHandlerBase，手动目标处理 | 继承EffectHandlerBase + ITargetProcessingService | ✅ [COMPLETED] |

### 已完成重构的Handler类 [COMPLETED]
这些Handler类在重构开始前就已经符合目标架构：

| Handler类 | 状态 | 完成状态 |
|-----------|------|----------|
| DamageEffectHandler | 继承EffectHandlerBase + ITargetProcessingService | ✅ [COMPLETED] |
| EvolveEffectHandler | 继承EffectHandlerBase + ITargetProcessingService | ✅ [COMPLETED] |

## 6. 重构成果总结

### 架构统一成果
- **总计Handler类**: 14个
- **重构完成**: 14个 (100%)
- **架构模式**: 全部遵循EffectHandlerBase + ITargetProcessingService模式
- **编译状态**: 所有EffectHandler相关编译错误已解决

### 代码质量提升
- **代码复用性**: 通过ITargetProcessingService统一目标处理逻辑
- **可测试性**: Handler逻辑更聚焦，Action委托可独立测试
- **可维护性**: 统一的架构模式，降低维护成本
- **错误处理**: 统一的错误处理和事件聚合机制

### 技术债务清理
- **文件扩展名问题**: 修复EffectResult文件缺少.cs扩展名
- **Logger使用统一**: 所有Handler使用基类Logger属性
- **构造函数标准化**: 统一使用非泛型ILogger参数
- **方法签名统一**: 所有ApplyAsync方法签名一致

## 7. 后续维护建议

### 新Handler开发规范
1. **必须继承**: `EffectHandlerBase<TParams>`
2. **必须使用**: `ITargetProcessingService`进行目标处理
3. **Action委托模式**: 将核心逻辑封装到Action委托中
4. **事件驱动**: 在Action委托内生成领域事件
5. **错误处理**: 通过IndividualActionResult返回处理结果

### 代码审查要点
- 确保新的Handler遵循统一架构模式
- 验证Action委托的独立性和可测试性
- 检查领域事件的正确生成和聚合
- 确认日志记录格式的一致性

---

## 8. 第三阶段：原子化重构 (Effect Atomization) [IN PROGRESS]

### 8.1. 背景与目标

在完成了前两个阶段的架构统一后，我们进入了更深层次的重构：将效果（Effect）处理彻底原子化。当前部分 `EffectHandler` 仍然处理着复合逻辑（例如，`DamageEffectHandler` 同时处理伤害计算、生命值修改、死亡检查），这违反了单一职责原则，并使得事件的时序管理变得复杂。

**本阶段目标：**

1.  **实现真正的原子性：** 将每个 `EffectHandler` 的职责限制在**一个不可再分的、最小的游戏状态变更**上。
2.  **确立“一对一”原则：** 建立一个 `EffectHandler` 只生成一种主要领域事件（`IGameEvent`）的清晰模式。
3.  **推广效果链（Effect Chain）：** 使用效果链来编排由多个原子效果组成的复杂游戏逻辑，以精确控制执行顺序和时序。
4.  **明确领域职责：** 进一步明确领域实体（如 `PlayerState`）和 `EffectHandler` 之间的职责边界。

### 8.2. 核心设计原则

1.  **原子效果 (Atomic Effect):**
    *   一个原子效果只对应一个最小化的状态变更。
    *   **示例:** `ModifyHealthEffect` 只负责修改生命值；`MoveCardEffect` 只负责将卡牌从一个区域移动到另一个区域。

2.  **一对一事件生成 (One-to-One Event Generation):**
    *   一个原子 `EffectHandler` 在其执行成功后，**必须只生成一个**与之对应的、描述“既成事实”的领域事件。
    *   **示例:**
        *   `ModifyHealthEffectHandler` -> `HealthChangedEvent`
        *   `MoveCardEffectHandler` -> `CardMovedEvent`
        *   `AddKeywordEffectHandler` -> `KeywordAddedEvent`
    *   **严禁**一个 Handler 生成多种不同类型的事件。例如，`ModifyHealthEffectHandler` **不应**生成 `UnitDestroyedEvent`。

3.  **效果链编排 (Orchestration via Effect Chains):**
    *   所有复杂的、多步骤的逻辑都**必须**通过效果链来实现。
    *   一个上层逻辑（如 `Ability` 的工厂方法或领域实体的业务流程方法）负责创建并返回一个“父效果”。这个父效果本身可能不执行任何操作，但其 `ChainedEffects` 列表包含了所有需要按顺序执行的原子效果。
    *   **示例：** “造成2点伤害”这个动作，将由一个父效果编排，其子效果链为：`[ModifyHealthEffect, CheckIfDestroyedEffect]`。

4.  **职责边界:**
    *   **领域实体 (`PlayerState`, `CardInstance`):** 负责执行**最核心的状态修改**，并返回包含**单个原子事件**的 `DomainOperationResult`。例如，`card.TakeDamage(amount)` 方法内部只会修改 `CurrentHealth` 并返回一个包含 `HealthChangedEvent` 的结果。
    *   **EffectHandler:** 职责被大幅削减，成为一个非常薄的“适配器”或“调用者”。它的唯一工作就是：
        1.  解析 `Effect` 对象中的参数。
        2.  调用 `ITargetProcessingService`。
        3.  在传递给服务的委托中，调用目标领域实体（如 `player` 或 `card`）上对应的**原子性**方法。

### 8.3. 重构范例：拆解“抽卡 (Draw Card)”

以 `DrawCardEffectHandler` 为例，展示如何将其从一个复合处理器重构为由效果链驱动的原子操作。

#### **重构前 (复合逻辑):**

一个 `DrawCardEffectHandler` 可能包含以下所有逻辑：
1.  检查牌库是否为空。
2.  从牌库顶部取牌。
3.  检查手牌是否已满。
4.  如果手牌未满，将卡牌加入手牌。
5.  如果手牌已满，将卡牌置入墓地（爆牌）。
6.  生成一个 `CardDrawnEvent` 或 `CardDiscardedEvent`。

#### **重构后 (原子效果链):**

现在，不再有 `DrawCardEffectHandler`。取而代之的是，一个更高层的逻辑（例如 `PlayerState.DrawCards` 方法或一个“抽牌能力”）会创建一个效果链。

**`PlayerState.DrawCards(amount)` 方法的职责:**
此方法不再直接修改状态，而是**创建并返回一个效果链**。

```csharp
// 在 PlayerState.cs 中 (示意代码)
public DomainOperationResult DrawCards(int amount)
{
    var result = new DomainOperationResult();
    
    // 创建一个代表“抽X张牌”这个宏观动作的父效果
    var drawChainEffect = new Effect(this.PlayerId) {
        // 这个父效果本身不执行逻辑，但可以携带完成事件
        CompletionEvents = { new CardsDrawnProcessCompletedEvent(this.PlayerId, amount) }
    };

    for (int i = 0; i < amount; i++)
    {
        // 为每一次抽牌创建一个子效果链
        var singleDrawChain = new Effect(this.PlayerId);
        
        // 1. 从牌库顶移动一张牌到“虚空”或“处理中”区域
        singleDrawChain.ChainedEffects.Add(SystemEffects.MoveTopCardOfDeck(this.PlayerId, CardZone.Limbo));
        
        // 2. 检查手牌上限
        singleDrawChain.ChainedEffects.Add(SystemEffects.CheckHandSize(this.PlayerId));
        
        // 3. 根据检查结果，条件性地移动卡牌
        //    (这部分可以通过带条件的Effect或在GameService中处理)
        //    - 如果手牌未满 -> MoveCard(from: Limbo, to: Hand)
        //    - 如果手牌已满 -> MoveCard(from: Limbo, to: Graveyard)
        
        drawChainEffect.ChainedEffects.Add(singleDrawChain);
    }

    result.Effects.Add(drawChainEffect);
    return result;
}
```

**新的原子 `EffectHandler`:**
*   `MoveCardEffectHandler`: 只负责将卡牌从一个区域移动到另一个区域，并生成 `CardMovedEvent`。
*   `CheckHandSizeEffectHandler`: 只负责检查手牌数量，并可能将结果（如 `IsFull`）存入 `GameContext` 的临时变量中，供后续效果使用。它生成 `HandSizeCheckedEvent`。

通过这种方式，我们将复杂的流程分解为一系列简单、可预测、可重用且易于测试的原子步骤。