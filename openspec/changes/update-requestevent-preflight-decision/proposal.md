# Change: Update RequestEvent Preflight Decision Flow

## Why
当前 Choose/Random 的决策主要发生在提交路径，导致两个问题：
1. `RewriteEventParamsEffect` 对决策参数（如 `SelectionCount`）的改写不能稳定反映到“预选择”界面。
2. 玩家主能力可撤回阶段与决策执行时机错位，出现“已执行部分效果后才允许取消”的语义冲突。

业务目标是：改写后的选项数量应直接出现在预选择阶段，并且在玩家主能力可撤回窗口内可取消。

## What Changes
- 引入标准动作管线：`RequestEvent -> ActionRoot(IPlayerActionEvent) -> DerivedEvents`。
- 为 `RequestEvent -> ActionRoot` 增加预处理决策阶段（Preflight），在提交前生成并解析 `ChooseEvent/RandomEvent`。
- 在 Preflight 阶段执行 Before 拦截（含 `RewriteEventParamsEffect`），确保改写后的决策参数用于玩家选择。
- 将可取消语义限定在 Preflight 阶段；提交阶段对同一决策仅复用缓存结果并禁止取消。
- 规范 RequestEvent 的准备语义：提交前不产生不可逆状态变更，避免撤回冲突。
- 增加决策输入签名校验机制；Preflight 与 Commit 输入不一致时中止事务并返回可诊断错误。

## Impact
- Affected specs: `event-decision`
- Affected code:
  - `Server/Services/GameActionTransactionService.cs`
  - `Server/Domain/Events/ActionRequests/*.cs`
  - `Server/Domain/Entities/PlayerState.cs`
  - `Server/Domain/Entities/CardInstance.cs`
  - `Server/Domain/Abilities/Ability.cs`
  - `Server/Infrastructure/Services/JsonGameEventRawEventMapper.cs`
  - `Server/Domain/Events/ActionRequests/*.cs`
  - `Server/Domain/Events/IGameEvent.cs`

## Out Of Scope
- 不实现全量历史快照回滚引擎。
- 不调整现有 Effect/Trigger DSL 语法。
- 不更改回放存储介质与序列化协议版本。
