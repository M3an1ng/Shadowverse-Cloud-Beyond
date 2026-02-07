# Change: Unify query-capable DSL expressions

## Why
当前 `.query/.orderBy/.take` 只能在目标配置的 QueryDslParser/QueryExecutor 链路中使用，条件与效果参数仍走 DslCompiler，导致同一语法在不同位置表现不一致，玩家学习成本高且容易出错。同时需要让 AbilityTargets 在所有表达式中可访问。

## What Changes
- 在 DslCompiler 中原生支持 `.query/.orderBy/.take` 作为集合操作，适用于目标、条件与效果参数。
- 统一 DSL 求值入口，条件/效果参数也使用 TSC-aware 编译以暴露 AbilityTargets。
- 保留 QueryDslParser/QueryExecutor 作为兼容层，必要时将旧查询 DSL 适配到统一表达式路径。

## Impact
- 受影响的 specs: `ability-dsl`
- 受影响的代码: `Server/Domain/Abilities/DSL/*`, `Server/Domain/Query/*`, `Server/Domain/Abilities/Conditions/*`, `Server/Domain/Abilities/Effects/*`
