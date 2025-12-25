## ADDED Requirements
### Requirement: 可持久化的已提交事件日志（Journal）
系统 MUST 提供可持久化的“已提交事件日志（Committed Event Journal）”，以支持按顺序读取与按 `EventId` 回查，并作为录像与观战的权威数据来源。

#### Scenario: 事务提交后追加写入事件包
- **WHEN** 一次玩家动作事务成功提交并产生事件序列
- **THEN** 系统将该序列作为一个 Committed Bundle 追加写入 Journal，且分配单调递增的 `Seq`

#### Scenario: 按范围读取事件用于补流
- **WHEN** 客户端请求 `fromSeq=100 limit=200`
- **THEN** 系统按序返回 `[100, 299]` 范围内可用的事件（不足则返回到 head）

#### Scenario: 按 EventId 回查触发事件
- **WHEN** 系统需要通过 `TriggeredEventId` 回查历史事件
- **THEN** 系统可从 Journal（或其缓存视图）返回对应事件记录，且不依赖进程内状态

### Requirement: 录制事件格式（RecordedEvent）与版本化
系统 MUST 使用稳定的纯数据录制格式保存事件（RecordedEvent），并通过 `SchemaVersion` 管理演进；录制格式 MUST NOT 直接序列化包含运行态引用的对象图。

#### Scenario: RecordedEvent 不包含运行态引用
- **WHEN** 一个领域事件被记录到 Journal
- **THEN** 其录制 payload 仅包含稳定标识与原子字段（如 `cardInstanceId/playerId/amount`），不包含 `CardInstance/PlayerState` 等引用对象

#### Scenario: SchemaVersion 兼容回放
- **WHEN** 系统加载旧版本录像数据
- **THEN** 回放器根据 `SchemaVersion` 选择兼容解析路径或明确拒绝并返回可诊断错误

### Requirement: 快照（Snapshot）用于观战入场与录像跳转
系统 MUST 提供快照存储（Snapshot Store），以支持观战快速入场与录像跳转回放（seek），并能在给定 `targetSeq` 时返回 `<= targetSeq` 的最近快照。

#### Scenario: 观战入场返回快照与 headSeq
- **WHEN** 观战者加入一局进行中的对战
- **THEN** 系统返回最新快照及 `headSeq`，观战者可从 `snapshotSeq+1` 补流追上实时

#### Scenario: 跳转回放从最近快照追赶
- **WHEN** 回放器请求跳转到 `targetSeq`
- **THEN** 系统返回 `atSeq <= targetSeq` 的最近快照，并允许拉取 `(atSeq, targetSeq]` 的事件范围用于追赶

### Requirement: EventHistory 与 Replay 的联动复用
`EventHistory` MUST 与 Replay/Jounal 复用同一份权威数据：EventHistory 作为 Journal 的缓存/索引视图提供 `EventId` 回查能力，禁止出现“双写两份事实来源”导致不一致。

#### Scenario: EventHistory cache miss 回源 Journal
- **WHEN** EventHistory 缓存未命中某个 `EventId`
- **THEN** 系统从 Journal 回源并返回结果，同时可回填缓存以优化后续查询

### Requirement: 可靠性与清理策略
系统 MUST 提供可配置的录像保留与清理策略，避免数据无限增长，并在写入失败时具备明确的错误策略以保证系统行为可预期。

#### Scenario: 按保留窗口清理旧录像
- **WHEN** 录像数据超过配置的保留窗口（如 30 天）
- **THEN** 清理任务删除对应录像数据并记录删除量与耗时指标

#### Scenario: Journal 写入失败的事务策略明确
- **WHEN** Journal 写入失败
- **THEN** 系统按配置策略（对局优先/录像完整优先）采取一致的处理方式，并输出可诊断日志

