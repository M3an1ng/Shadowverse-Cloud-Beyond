## 1. Implementation
- [ ] 1.1 在事务层引入 `PreflightSession` 与 `CommitPlan` 结构，并在单次事务内贯通存取。
- [ ] 1.2 为主能力预检添加统一步骤编排（条件、目标、决策、分支、效果物化）。
- [ ] 1.3 将 `Choose/Random` 分支能力纳入递归预检流程。
- [ ] 1.4 将分支能力中的目标选择也纳入预检，并支持取消。
- [ ] 1.5 在提交阶段启用“仅消费预检结果”模式，禁止发起新的玩家交互请求。
- [ ] 1.6 增加预检/提交一致性校验（决策输入签名、目标签名、状态指纹）。
- [ ] 1.7 清理旧的分散预检判断与重复解析代码路径，保留兼容适配层。

## 2. Tests
- [ ] 2.1 集成：非目标选择入场曲“战场所有随从”在提交时包含自身，且不提前锁定错误目标。
- [ ] 2.2 集成：`Choose` 主能力在预检阶段可取消，取消后无不可逆状态变更。
- [ ] 2.3 集成：`Choose` 分支目标选择也在预检阶段完成并可取消。
- [ ] 2.4 集成：提交阶段不允许新交互；若缺失预检快照则事务失败并返回诊断信息。
- [ ] 2.5 集成：预检与提交签名不一致时事务失败，且无部分提交。

## 3. Validation
- [ ] 3.1 `dotnet build Server/ShadowverseBeyond.Server.csproj -v minimal`
- [ ] 3.2 `dotnet test Server.Tests/ShadowverseBeyond.Server.Tests.csproj --filter FullyQualifiedName~TransactionService`
- [ ] 3.3 `openspec validate refactor-unified-preflight-orchestration --strict`
