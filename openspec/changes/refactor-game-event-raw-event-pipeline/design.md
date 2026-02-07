# Design: RawEvent（输入/输出）+ 现有强类型 IGameEvent（运行期）

## 1. Goals
满足以下四项需求并尽量降低 DSL 与回放系统的复杂度：
1) **Source/Target 初始解析步骤**：输入/回放只携带 ID，运行期必须在 DSL 判断前解析为 TargetInfo。
2) **事件参数区分输入/输出（两部分）**：输入用于意图执行前（Before/On 的条件与目标访问）；输出用于 Commit 后（On 的效果与 After 访问），并作为回放恢复权威事实。
3) **triggeringEvent 作为唯一可靠源事件**：能力触发时可稳定读取触发事件的全部参数（含输入/输出）与已解析 Source/Target。
4) **DSL 事件参数访问 API + 编辑器自动提示**：编辑器可按事件类型列出可访问参数（含类型），并在运行期用统一 API 获取。

## 2. Non-Goals
- 不在本变更中实现完整的“命令录制 + 可重演随机数”的输入重放体系（仍以事实事件回放为主）。
- 不要求一次性重构所有事件类型；允许以“渐进式迁移”方式落地（先覆盖关键 Committable 事件与调试入口）。

## 3. Terminology
- **RawEvent**：纯数据事件载体（只含稳定 ID 与原子字段 payload），可序列化与版本化。
- **Input RawEvent**：外部输入的最小事件；仅包含执行所需的输入参数。
- **Output RawEvent**：Commit 之后生成的事实事件；包含回放所需的输出参数（含随机结果/派生结果）。
- **TypedEvent / IGameEvent**：运行期强类型事件对象，具备 TargetInfo（方便 DSL 访问实体属性）。
  - 这里的“强类型事件”不引入新的基类/统一包装类型；直接复用现有 `IGameEvent` 与各个具体事件类实例。

## 4. Data Model（concept）

### 4.1 RawEvent (v1)
建议将 RawEvent 作为一个 JSON 结构，字段均为稳定类型：
- `schemaVersion` (int)
- `eventType` (string 或 int；建议与 EventType enum 有稳定映射)
- `eventId` (Guid, server assigned)
- `timestampUtc` (DateTime, server assigned; output 必填，input 可省略)
- `sourceId` (Guid?; nullable)
- `targetIds` (Guid[]; 可为空数组)
- `payload` (object; 仅允许原子类型、数组、嵌套对象；禁止运行态引用)

### 4.2 Input/Output 的区分方式
- 输入与输出使用同一 RawEvent 结构，但语义不同：
  - Input：`payload` 表示“执行所需输入”；可缺省 `eventId/timestampUtc`（由服务端补齐）。
  - Output：`payload` 表示“执行结果（事实）”；必须包含回放恢复所需字段，尤其随机结果必须写入。

## 5. Pipeline（严谨时序）
1) **Receive Input RawEvent**
   - 来源：调试指令、测试注入、（未来）客户端动作输入
   - 先做 schema 与字段校验：必填字段、允许字段、payload 结构与类型
2) **RawEvent → IGameEvent（Parse/Map）**
   - 通过注册表：`eventType -> (parse input payload -> typed event)`。
   - 解析阶段只负责把输入参数塞到 typed event 的“Input 参数”区域（见 §6）。
3) **Resolve Source/Target（ID → TargetInfo）**
   - 在进入任何 DSL 判断（Before/On）前必须完成。
   - 无法解析时：返回可诊断错误（不入管线）。
4) **Before/On（条件 + 目标部分）**
   - DSL 允许访问：触发事件输入参数 + 已解析 Source/Target。
5) **Commit**
   - 允许产生随机数、派生结果、子事件等。
6) **填充 Output 参数 + 生成 Output RawEvent**
   - 输出参数必须完整，足以回放恢复（包含随机结果）。
   - Output RawEvent 进入 Journal，作为回放与观战的权威事实源。
7) **After**
   - DSL 允许访问：触发事件输出参数 + 已解析 Source/Target + 已选目标等上下文。

## 6. Input/Output 参数的落位（IGameEvent 实例）
建议每个事件类型以“输入/输出两部分”声明参数，避免把“执行结果”混入“执行前输入”：
- **InputParams**：意图执行所需参数（Before/On 可读）
- **OutputParams**：Commit 后补齐的执行结果（On 的效果与 After 可读）

简单事件允许 `OutputParams == InputParams`（概念上相同；实现上可复用同一字段或标记为 Both）。

## 7. TriggeringEvent 的一致性要求
- `GameContext.TriggeringEvent` MUST 指向“触发当前能力的 TypedEvent 实例”。
- TriggeringEvent MUST 在该能力的整个执行链路中稳定可读（Before/On/After），并携带输入/输出参数与已解析 Source/Target。
- 连锁能力读取父能力输出时，优先通过 `TriggeringEvent` 或 `CurrentEffect` 的输出引用完成（避免依赖不稳定的“上一条 EffectCompletedEvent 的查找”）。

## 8. DSL Event Parameter Access（与编辑器联动）
- 运行期：提供 DSL 函数/API 访问 TriggeringEvent 的输入/输出参数（按 name），并提供类型安全的变体（int/bool/string/guid）。
- 编辑器：对每个事件类型暴露“可访问参数列表”，包含：
  - 参数名（DSL 访问名）
  - DisplayName/Description
  - 类型（含是否可空、集合等）
  - 参数归类：Input / Output / Both

## 9. Migration Strategy（建议）
1) 先引入 RawEvent 模型与注册表，但保留现有 DTO/事件输入路径。
2) 将调试指令入口切换为“生成 Input RawEvent → 映射 TypedEvent”。
3) 将 Replay Journal 的录制格式逐步切换为 Output RawEvent（稳定 schemaVersion + payload），并保留旧格式读取的兼容层（至至少一个版本窗口）。
4) 为关键事件（如 Damage/Draw/PlayCard/Attack/MulliganSubmitted）补齐参数元数据与 Input/Output 归类。
