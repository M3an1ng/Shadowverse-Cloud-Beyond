# Change: 以 RawEvent 统一事件输入/回放，并保持运行期强类型事件用于 DSL

## Why
- 当前“指令输入 / 回放 / 运行期事件（IGameEvent）”之间缺少统一的权威数据模型：输入侧需要最小参数，回放侧需要稳定的事实事件，而运行期事件为了 DSL 友好又需要已解析的 Source/Target（TargetInfo）。
- 回放/观战当前依赖“序列化客户端 DTO + 类型名”的 best-effort 录制方式，存在跨版本不稳定、难以演进与诊断的问题。
- DSL 侧希望可靠读取“触发事件”的参数（例如 Damage.amount），并且前端编辑器希望能自动识别不同事件可访问的参数，减少 DSL 学习与误用成本。

## What Changes
- 引入 **RawEvent（纯数据、可版本化）** 作为事件**输入**与**输出（事实）**的权威依据：
  - **Input RawEvent**：用于调试指令/测试注入（以及后续可扩展的客户端输入），只要求最小参数（SourceId/TargetIds + Payload(Input)）。
  - **Output RawEvent**：用于回放/观战记录（facts），由服务器在 Commit 后生成（SourceId/TargetIds + Payload(Output) + SchemaVersion 等）。
- 保持运行期仍以 **现有强类型 `IGameEvent`（含 TargetInfo）** 执行与 DSL 访问；通过 **RawEvent → `IGameEvent` 实例**的映射/注册表完成解析与校验，并在 Commit 后生成 Output RawEvent。
- 明确事件生命周期中的时点：`RawEvent → Resolve Source/Target → Before/On DSL → Commit → After DSL → 生成 Output RawEvent`，并规定随机数等“执行结果”必须进入 Output。
- 以 `GameContext.TriggeringEvent` 作为唯一可靠的触发事件信息来源：触发能力在所有时点均可读取到该事件的输入/输出参数与 Source/Target。
- 为 DSL 增加“事件参数访问”API，并扩展事件元数据：前端编辑器可按事件类型给出可访问的输入/输出参数提示（含类型/描述）。

## Impact
- Affected specs (new): raw-event-pipeline, dsl-event-params
- Affected code (planned): 事件基类/事件映射注册表、DebugCommandParser 与调试指令注入入口、Replay/EventJournal 录制格式、回放 rehydrator、DSL 函数与元数据聚合接口。
