# Design: Ability-Related Guid Stability (Cross-Process Replay Oriented)

## 1. Guid Inventory（现状调查）

### 1.1 Ability 运行时
- `Ability.RuntimeId : Guid`
  - 生成：`Guid.NewGuid()`（Ability 构造时）
  - 用途：
    - `AbilityManager` 运行期索引：`GetAbilityByRuntimeId(runtimeId)`
    - `Buff.SourceAbilityId` 指向来源 Ability 实例（用于反查来源）
    - `Effect.sourceAbilityId` 指向父 Ability RuntimeId（用于 effect 执行上下文反查当前 Ability）
    - `PlayerChoiceRequest.SourceAbilityRuntimeId` / `SubmitTargetChoiceActionDto.SourceAbilityRuntimeId`：用于客户端提交选择时定位所属能力
  - 约束：仅在同一局、同一服务进程的内存生命周期内有效；不应写入回放/存档作为权威 ID。

### 1.2 Effect 队列
- `Effect.Id : Guid`
  - 生成：`Guid.NewGuid()`（默认字段初始化）
  - 用途：日志/诊断（例如 transaction service 输出 effectId）
  - 约束：运行期临时标识，不应持久化。

- `Effect.TriggeredEventId : Guid`
  - 来源：`context.TriggeringEvent?.EventId`（事件触发能力时传入）
  - 用途：在 `ProcessEffectAsync` 中从 `processedEvents` 找到当时的 triggeringEvent，用于 DSL 与 handler 上下文精确化
  - 约束：该 Guid 只在当前事务的 `processedEvents` 集合中有意义；不是跨局的稳定引用。

### 1.3 Pending Action（玩家选择恢复）
- `PlayerChoiceRequest.PendingChoiceId : Guid`
  - 生成：`Guid.NewGuid()`
  - 用途：客户端提交 `SubmitTargetChoiceActionDto.PendingChoiceId` 时用于恢复等待的选择
  - 约束：同一局/同一进程内稳定即可；不作为回放依赖。

### 1.4 Buff
- `Buff.Id : Guid`
  - 生成：`Guid.NewGuid()`（实例标识）
  - 用途：Buff 实例区分、移除/替换、日志

- `Buff.SourceAbilityId : Guid`
  - 现状：构造函数强制非空（`Guid.Empty` 直接抛错），导致“系统路径”只能用 `Guid.NewGuid()` 伪造来源（例如无 ability 上下文的 `StatChangedEvent`）
  - 用途：
    - 通过 `GameServiceProvider.CurrentGameContext?.AbilityManager.GetAbilityByRuntimeId(SourceAbilityId)` 反查来源 Ability
  - 问题：
    - 系统 Buff 的来源语义不清晰（随机 Guid 既无法反查，也污染日志）
    - 未来做离线回放时，此类随机来源没有任何价值，反而易造成误解

## 2. Stability Matrix（跨进程回放视角）
| 标识 | 当前形态 | 是否需要跨进程稳定 | 原因 | 推荐策略 |
|---|---|---:|---|---|
| `CardInstanceId` | `Guid`（运行时生成） | ✅ | 事件/目标大量引用卡实例；跨进程重建需要对齐实体引用 | 引入可注入 ID 生成器或允许“按日志指定 ID 创建实例” |
| `Ability.RuntimeId` | `Guid`（运行时生成） | ⚠️（尽量不要） | 仅用于运行时索引/选择恢复；跨进程稳定会把运行时细节变成持久契约 | 用稳定键替代（`DefinitionId + SourceCardInstanceId + SourceBuffId?`），运行时仍保留 RuntimeId |
| `Effect.Id` | `Guid`（运行时生成） | ❌ | 仅诊断用途；不应进入回放权威 | 保持运行时随机即可；不要持久化/协议化 |
| `Buff.Id` | `Guid`（运行时生成） | ✅（如果 Buff 需要被后续事件引用） | `BuffRemoved`/叠加替换等需要定位具体 Buff 实例；跨进程重建必须一致 | 让 Buff.Id 可从“事实事件”输入（可注入生成器/或日志指定） |
| `Buff.SourceAbilityId` | `Guid`（目前用 RuntimeId） | ⚠️（尽量不要） | 用于叠加替换（同来源）、日志；跨进程稳定会绑定到 RuntimeId | 改为“稳定来源键”，或在回放中可从事实事件重建该关系 |
| `PendingChoiceId` | `Guid` | ❌ | 只用于在线交互恢复，不属于回放 | 保持运行时随机即可 |

注：`✅` 表示跨进程离线重放/重建时需要确定性；`⚠️` 表示应避免把其变为持久契约，优先用稳定键替代。

## 2.1 Human-Friendly In-Game Ids（调试/日志可读性）
当我们为“可重放引用”引入/替换实例标识（例如 CardInstanceId/BuffId），除了跨进程稳定之外，还需要满足调试与日志的可读性：
- **规律生成**：同一局内单调递增/可预测（便于测试复现与手工输入）。
- **可手输**：短、大小写不敏感、避免易混字符（如 `0/O`、`1/I/l`）。
- **可识别类别**：从 ID 格式即可判断对象类别（Card/Buff/Ability/Player/…）。

### 2.1.1 格式约束（局内唯一）
- 规范展示格式：`<PREFIX>-<NUMBER>`，其中：
  - `<PREFIX>` 为对象类别前缀（大小写不敏感）
  - `<NUMBER>` 为十进制正整数（`> 0`），展示时默认 `D6` 补零（例如 `000123`）
- 当前支持前缀（可扩展）：`C`(Card) `B`(Buff) `A`(Ability) `E`(Effect) `P`(Player) `EV`(Event) `CH`(Choice)
- 解析容错（便于手输）：允许省略连字符（例如 `C000123`），以及省略前导零（例如 `C-1`）

### 2.1.2 生成与映射策略（不破坏现有 Guid 协议）
- **不立即替换现有 Guid**：局内仍以 `Guid` 作为内部/协议标识（兼容现有 DTO/前端），同时为调试/日志额外生成可读 ID。
- **每局独立映射表**：在 `GameState` 维护 `Guid <-> InGameId` 的双向映射（首次见到时按类别计数递增分配）。
- **日志输出**：默认同时输出 `ReadableId` 与 Guid（例如 `C-000123 (guid=...)`），便于定位与复制。
- **调试指令输入**：支持可读 ID 与 Guid 两种输入；当输入为可读 ID 时，通过映射表还原到 Guid。

（以上策略已涵盖日志/调试指令的输出与输入约定。）

## 3. 目标语义（本变更约定）
- **运行期临时 Guid**：`Ability.RuntimeId`、`Effect.Id`、`PendingChoiceId`、`TriggeredEventId` 等只用于“同局/同进程”关联，MUST NOT 成为回放权威依赖。
- **可重放标识**：当某个实例 ID 会被后续事实事件引用（例如 Buff 的移除/替换需要定位 Buff 实例），它 MUST 可由事实事件驱动重建（稳定/可注入）。
- **回放权威依据**：事实事件（Output RawEvent）+ 稳定身份（例如 `Ability.DefinitionId`、卡牌定义 id、玩家 id、以及需要被引用的实例 id）。
- **System/No-Source Buff**：当 Buff 不由任何 Ability 实例产生时，应使用一个清晰的标记而非随机 Guid。

## 3. 方案决策

### 3.1 Buff.SourceAbilityId 允许 `Guid.Empty` 表示 System/No-Source（推荐）
- 调整 `Buff` 构造函数：允许 `Guid.Empty`，并把它定义为“无来源能力”。
- `Buff.SourceAbility`：当 `SourceAbilityId == Guid.Empty` 时必须返回 `null`。
- 所有消费方（日志/效果/移除逻辑）不得把 `Guid.Empty` 当作可反查来源 Ability 的有效 ID。

优点：
- 最小侵入：不需要把 `Guid` 改成可空类型，不破坏大量签名。
- 语义明确：系统 Buff 不再制造随机来源。

风险：
- 需要系统性检查是否有代码把 “`Guid.Empty` 不可能出现” 当作前提（通过单测覆盖关键路径）。

### 3.2 Buff/Ability 的跨进程稳定策略（本变更提供指导，不强制一次性实现）
- **Buff.Id**：如果后续事件需要定位 Buff 实例，则必须让 Buff.Id 可由事实事件驱动重建。
  - 例如：在 `BuffApplied` 的 Output RawEvent payload 中记录 `BuffId`，`BuffRemoved` 引用同一个 `BuffId`；
  - 并提供创建路径允许“指定 BuffId 创建/注册”。
- **Ability 来源标识**：不建议让 `Ability.RuntimeId` 变成跨进程稳定契约；推荐引入稳定键（例如 `AbilityInstanceKey`）用于 Buff 叠加/替换等“来源识别”。

### 3.3（非目标）统一改为全局确定性 ID 生成器
本变更不要求立即引入全局确定性 ID 生成体系；如果未来实现离线回放/跨进程重建，会在单独变更中引入可注入的 `IIdGenerator` 或“从日志指定 ID 创建”的机制，并将 `CardInstanceId/Buff.Id` 纳入该体系。

## 4. 测试与验证要点
- 系统路径创建的 Buff 使用 `Guid.Empty` 时：
  - Buff 可正常注册/生效/移除
  - `Buff.SourceAbility == null`
  - 不影响 Buff 叠加规则与移除规则
- 玩家选择恢复：`PendingChoiceId` / `SourceAbilityRuntimeId` 仍按现有逻辑工作（不改变）。

- 若后续实现跨进程回放稳定性：
  - Buff 相关事实事件在回放重建时能稳定定位同一个 Buff 实例（`BuffApplied`/`BuffRemoved` 对齐）
  - 不要求 `Effect.Id`/`PendingChoiceId` 在回放中一致
