# 当前活动背景 (Active Context)

## 1. 当前任务/焦点 (Current Task/Focus)

*   **主要目标:** 修复 `Server.Tests` 项目的编译错误。
*   **背景:** 在完成 `CardQueryExecutor` 的骨架实现并通过编译后，需要确保测试项目能够正常编译，以便为后续的功能实现（如复杂筛选器）提供测试保障。

## 2. 近期决策/变更 (Recent Decisions/Changes)

*   `CardQueryExecutor.ApplyOrdering` 修复了因 `OrderingConfig.OrderBy` 类型为 `FilterType` 而非 `string` 导致的编译错误。
*   `CardQueryExecutor.ApplyOrdering` 修复了与 `Comparer<object?>` 相关的可空性警告。
*   为 `CardQueryExecutor` 中未实现的复杂筛选器 (`HasEffect`, `IsTargetable`, `And/Or/Not`) 添加了日志占位符。
*   为 `CardQueryExecutor` 中的 `ApplyPostProcessing` 和 `ApplyLimit` 方法实现了基本骨架。
*   验证了 `CardInstanceDto` 中存在 `CardQueryExecutor` 所需的属性，确认无需修改相关逻辑。
*   创建了 Memory Bank 的核心文件 (`projectbrief.md`, `productContext.md`, `systemPatterns.md`, `techContext.md`, `activeContext.md`, `progress.md`)。
*   更新了 `progress.md` 以反映最新进展。

## 3. 阻塞/风险/问题 (Blockers/Risks/Issues)

*   **Memory Bank MCP 工具路径问题:** `memory_bank_read/write` 无法正常工作，当前使用 `read_file`/`write_to_file` 作为替代方案。
*   **`Server.Tests` 编译错误:** **(当前焦点)** 测试项目无法编译。
*   **复杂筛选器未实现:** `HasEffect`, `IsTargetable`, `And`, `Or`, `Not` 等筛选器尚未实现。
*   **测试覆盖率不足:** 即使编译通过，也需要为 `CardQueryExecutor` 等新功能添加单元测试或集成测试。

## 4. 后续计划 (Next Steps)

1.  **修复 `Server.Tests` 编译错误:** (当前任务) 诊断并解决测试项目中的编译问题。
2.  **(后续)** 为 `CardQueryExecutor` 添加单元测试。
3.  **(后续)** 实现复杂筛选器 (`And/Or/Not`, `HasEffect`, `IsTargetable`)。
4.  **(后续)** 实现 `ApplyPostProcessing` 和 `ApplyLimit` 中的动态源 (`CountSource`, `MaxCountSource`) 解析。
5.  **(后续)** 实现能力系统和事件处理流程。
6.  **(待定)** 初始化 Git 仓库。

*(注意: 此文档会频繁更新以反映当前工作状态)*