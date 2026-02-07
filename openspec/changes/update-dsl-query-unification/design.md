## Context
目前 QueryDslParser/QueryExecutor 只服务于目标配置链路，而条件/效果参数使用 DslCompiler。两套 DSL 能力不一致，`.query/.orderBy/.take` 无法在所有表达式中通用，并造成 AbilityTargets 可见性分裂。

## Goals / Non-Goals
- Goals:
  - 统一 DSL 语法能力，所有表达式都支持 `.query/.orderBy/.take`。
  - 让 AbilityTargets 在所有表达式上下文中可访问（通过 TSC-aware 编译）。
  - 保留旧 Query DSL 兼容路径，避免现有数据失效。
- Non-Goals:
  - 不改变现有 DSL 的基础语义（运算优先级、函数等）。
  - 不移除 QueryDslParser/QueryExecutor 的对外接口（保留兼容期）。

## Decisions
- 在 DslCompiler 的表达式树构建中增加集合操作支持：
  - `.query(expr)` -> `Enumerable.Where`（`expr` 用集合元素作为 self）。
  - `.orderBy(expr[, desc])` -> `OrderBy/ThenBy`。
  - `.take([first|last|random] n)` -> 选择策略 + `Take`。
- 统一所有 DSL 求值入口使用 TSC-aware 编译方法：
  - 条件与效果参数在缺少目标上下文时使用空 `TargetSelectionContext`。
  - AbilityTargets 始终指向 `TargetSelectionContext.ResolvedTargets`。
- QueryDslParser 作为兼容层：
  - 若输入为旧 Query DSL，转换为统一 DSL 表达式或在 QueryExecutor 内部委托 DslCompiler 执行。

## Alternatives considered
- 仅在 ValueSourceResolver/ConditionEvaluator 内部检测 `.query` 并分流到 QueryExecutor：实现简单，但仍保留双入口与语义分裂。

## Risks / Trade-offs
- 新增集合操作可能影响性能与缓存命中：需要保证表达式缓存仍然生效。
- 目标上下文注入范围扩大，需避免缓存委托持有旧 TSC 引用。

## Migration Plan
1. 为 DslCompiler 增加集合操作语义并补充单元测试。
2. 统一 ConditionEvaluator/ValueSourceResolver/TargetResolver 的 DSL 编译入口。
3. QueryDslParser 兼容转换或透传执行，补充回归测试。

## Open Questions
- `.orderBy` 是否需要完整支持多字段排序（当前 Query DSL 支持逗号分隔）。
- `random` 的随机源是否需要接入可控随机（以保证测试可重复）。
