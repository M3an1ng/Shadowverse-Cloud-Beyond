## ADDED Requirements
### Requirement: RawEvent 作为输入与回放的权威数据模型
系统 MUST 定义可版本化的 RawEvent（纯数据事件）作为“外部输入（Input）”与“事实回放（Output）”的权威依据。RawEvent MUST 只包含稳定 ID 与原子字段 payload，禁止直接序列化运行态对象图。

#### Scenario: Input RawEvent 仅携带最小参数
- **WHEN** 调试指令或测试注入提交一个事件
- **THEN** 系统接受仅包含 `eventType + sourceId/targetIds + payload(input)` 的 Input RawEvent（`eventId/timestampUtc` 可由服务端补齐）

#### Scenario: Output RawEvent 作为事实被持久化
- **WHEN** 一个事件成功 Commit 并产生事实结果
- **THEN** 系统生成 Output RawEvent，并将其写入 Journal 作为回放/观战的权威事实来源

### Requirement: RawEvent 严格校验与可诊断失败
系统 MUST 对 RawEvent 做严格格式约束与校验：缺少必填字段、出现未知字段、payload 类型不匹配时必须失败并返回可诊断错误；失败的 RawEvent MUST NOT 进入事务/事件管线。

#### Scenario: 缺少必填字段被拒绝
- **WHEN** Input RawEvent 缺少 `eventType` 或必需的 payload 字段
- **THEN** 系统返回“参数缺失或非法”的错误，并拒绝执行

#### Scenario: 出现未知字段被拒绝
- **WHEN** Input RawEvent 中包含未定义/未允许的字段
- **THEN** 系统返回“字段不支持”的错误，并拒绝执行

### Requirement: RawEvent → TypedEvent 映射注册表
系统 MUST 通过统一注册表将 RawEvent 映射为运行期强类型 `IGameEvent`（复用现有事件类实例），并在映射阶段完成参数解析（输入参数）。未知 `eventType` 或解析失败必须返回可诊断错误。

#### Scenario: 未注册 eventType 的 RawEvent 被拒绝
- **WHEN** 输入 RawEvent 的 `eventType` 在注册表中不存在
- **THEN** 系统返回“未知事件类型”的错误，并拒绝执行

### Requirement: Source/Target 在 DSL 前解析为 TargetInfo
系统 MUST 在进入任何 DSL 判断（Before/On）前，将 `sourceId/targetIds` 解析为 TargetInfo 并填充到 `IGameEvent` 实例的 `Source/Target`。解析失败必须阻断执行并返回可诊断错误。

#### Scenario: 解析成功后 DSL 可访问实体属性
- **WHEN** Input RawEvent 的 `targetIds` 指向一张在场随从
- **THEN** 系统在 DSL 判断前将其解析为 TargetInfo，使 DSL 可读取如“攻击力”等实体属性

### Requirement: 输入/输出参数两段式（Input/Output）
每个事件类型 MUST 定义输入参数与输出参数两段式语义：输入参数用于意图执行前（Before/On 条件与目标访问），输出参数用于 Commit 后（On 效果与 After 访问）并作为回放所需事实。随机数或派生结果 MUST 写入输出参数并进入 Output RawEvent。

#### Scenario: 抽牌事件把随机结果写入输出
- **WHEN** 一个抽牌事件 Commit 产生“抽到的牌实例”
- **THEN** 输出参数包含被抽到的牌的稳定标识，且 Output RawEvent 可用于回放恢复同一结果
