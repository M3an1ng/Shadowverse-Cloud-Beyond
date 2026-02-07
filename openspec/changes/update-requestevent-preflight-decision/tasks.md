## 1. Implementation
- [x] 1.1 `GameActionTransactionService.ProcessTransactionAsync` 新增前置步骤 `ExpandRequestToActionRootsAsync` 与 `PreflightPrimaryAbilitiesAsync`。
- [x] 1.2 `AnalyzePrimaryAbilitiesAsync` 改为处理 ActionRoot 列表，不再直接依赖 RequestEvent 类型。
- [x] 1.3 在 Preflight 路径复用 `PrepareDecisionEventAsync`，并确保先执行 `ApplyBeforeInterceptsAsync` 后再发起选择请求。
- [x] 1.4 `ResolveDecisionEventIfNeededAsync` 增加阶段参数（Preflight/Commit）：Preflight 可取消，Commit 不可取消。
- [x] 1.5 新增 `DecisionInputSignature` 计算与校验逻辑；Commit 阶段仅复用签名一致的 Preflight 决策缓存。
- [x] 1.6 `Ability` 决策缓存键改为执行实例维度（含 `EffectId` 与 `ExecutionIndex`），并补齐清理接口。
- [x] 1.7 `ProcessEffectAsync` 在 Repeat 多轮执行前显式清理对应执行实例决策缓存，保证逐轮重选/重随机行为可配置。
- [x] 1.8 清理提前状态写入：重构 `AttackRequestedEvent`/`EvolveRequestedEvent`/`SuperEvolveRequestedEvent` 及 `CardInstance`/`PlayerState` 对应方法，提交前不落地不可逆变更。
- [x] 1.9 对齐事件输出：`ChooseEvent/RandomEvent` 仅保留结果序号输出，来源卡牌通过 `Source` 投影暴露。
- [ ] 1.10 更新 `JsonGameEventRawEventMapper` 和事件元数据映射，确保预选择与提交后记录一致可回放。

## 2. Tests
- [ ] 2.1 集成：`RewriteEventParamsEffect` 改写 `SelectionCount` 后，预选择界面直接显示改写后的数量。
- [ ] 2.2 集成：Preflight 取消返回事务取消，且无不可逆状态落地（PP、攻击次数、进化权不变）。
- [ ] 2.3 集成：Commit 阶段复用 Preflight 决策结果，Choose 不可取消。
- [ ] 2.4 集成：Preflight 与 Commit 签名不一致时事务失败并返回明确错误。
- [ ] 2.5 集成：Repeat 场景每轮重新决策（或按配置复用）行为符合设计。
- [ ] 2.6 集成：AI/脚本路径走同一 Preflight 决策流程并可稳定执行。

## 3. Validation
- [x] 3.1 `dotnet build ShadowverseBeyond.Server.csproj -v minimal`
- [ ] 3.2 `dotnet test Server.Tests/ShadowverseBeyond.Server.Tests.csproj --filter FullyQualifiedName~TransactionServiceIntegrationTests`
- [x] 3.3 `openspec validate update-requestevent-preflight-decision --strict`
