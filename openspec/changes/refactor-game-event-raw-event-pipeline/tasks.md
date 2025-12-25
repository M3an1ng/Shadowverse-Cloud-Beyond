## 1. Design
- [x] 1.1 定义 RawEvent（Input/Output）数据结构与 schemaVersion 策略（字段、允许类型、校验规则）。
- [x] 1.2 定义 RawEvent ↔ `IGameEvent`（现有运行期强类型事件）的映射注册表与失败策略（未知 eventType/缺参/类型不匹配/目标解析失败）。
- [x] 1.3 定义 `IGameEvent`（现有事件类）的输入/输出参数落位方式（字段/属性/元数据标记），并明确 DSL 可访问边界。
- [x] 1.4 定义 TriggeringEvent 一致性规则（Before/On/After）与连锁能力读取父输出的统一方式。

## 2. Implementation (Server)
- [x] 2.1 新增 RawEvent DTO/Domain Model（与 Replay/Debug 共用），并实现严格校验（必填字段、payload 类型、未知字段拒绝）。
- [x] 2.2 新增映射注册表：按 EventType 注册解析器（Input RawEvent → `IGameEvent`）与录制器（`IGameEvent` → Output RawEvent）。
- [x] 2.2.0（落地）提供最小 RawEvent → `IGameEvent` 注册表（覆盖 debug 常用事件：Damage/Heal/Destroy/CardMoved/Draw/PP/EP）。
- [x] 2.3 在事件处理管线中引入“Resolve Source/Target”步骤：在任何 DSL 判断前，使用 SourceId/TargetIds 解析为 TargetInfo。
- [x] 2.4 改造调试指令入口：DebugCommandParser 只负责把命令转为 Input RawEvent（或直接构造 RawEvent）；具体事件构造由注册表完成。
- [x] 2.4.0（落地）将调试指令（damage/heal/destroy/vanish/return/draw/add-card/mana/pp/ep/event）改为“RawEvent → Registry → IGameEvent”路径。
- [x] 2.5 改造 Replay/EventJournal：写入 Output RawEvent（而非序列化客户端 DTO 类型名），并提供至少一个版本窗口的兼容读取。
- [x] 2.5.0（落地）在 ReplayEvents 中新增 RawEventJson，并在事务提交后生成并写入 Output RawEvent（不移除旧 ClientDtos，先并行保留）。
- [x] 2.5.1（兼容）保留旧 RecordedClientDtoEnvelope 读取路径，遇到旧数据时 best-effort 转换为 Output RawEvent 或明确拒绝并输出诊断信息。
- [x] 2.6 改造 Rehydrator：从 Output RawEvent 重建 `IGameEvent`（用于回放/回查/TriggeringEvent 一致性）。
- [x] 2.6.0（落地）Rehydrator 优先读取 RawEventJson 的 payload，确保回查/触发事件能读取参数。

## 3. Implementation (DSL + Metadata)
- [x] 3.1 增加 DSL 事件参数访问 API（Input/Output），并定义类型安全变体（int/bool/string/guid）。
- [x] 3.2 扩展事件元数据：对每个事件暴露“参数列表 + Input/Output/Both 归类”，供编辑器自动提示。
- [x] 3.3 聚合元数据接口：Ability Editor 元数据接口返回事件参数元数据与 DSL 函数元数据的一致视图。
- [x] 3.3.1（前端）更新前端编辑器：在事件选择 tooltip 中展示参数 Scope，并给出 TriggeringEvent 参数访问（eventInt/eventBool/...）示例。

## 4. Validation
- [x] 4.1 单测：RawEvent 校验（缺参/多参/类型错）与映射注册表行为（未知 eventType、解析失败）。
- [x] 4.2 单测：关键事件（Damage/Draw/MulliganSubmitted 等）Input/Output 参数归类与 DSL 访问结果一致。
- [x] 4.2.0（落地）新增单测覆盖：DSL `eventInt` 优先读取 RawEvent payload，事件元数据暴露 Scope（Draw/MulliganSubmitted）。
- [x] 4.2.1（落地）新增单测覆盖：`IGameEvent → OutputRawEvent` 映射能输出关键参数（Damage.Damage）。
- [x] 4.3 集成：Replay Journal 写入 RawEventJson 后，Rehydrator 可读取并恢复 payload（TriggeringEvent 参数可用）。
- [ ] 4.4 手动：调试控制台输入最小参数指令可正确触发事件；前端编辑器对不同事件展示正确参数提示。
