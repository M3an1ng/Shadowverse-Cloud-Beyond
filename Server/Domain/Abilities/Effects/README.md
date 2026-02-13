# Effect 系统使用指南（与当前代码一致）

本项目的 Effect 系统是“配置驱动 + 事件驱动”的执行层：能力根据配置生成 `Effect` 请求，事务引擎按顺序执行效果并产出事件。

## 关键对象

### `EffectConfig`
定义效果的静态配置（类型、参数、Outputs、ChainedAbilities、RepeatCount）。
- 位置：`Server/Domain/Abilities/Effects/EffectConfig.cs`
- `RepeatCount`：额外重复次数（0 表示只执行一次；>=1 时会重复执行并触发连锁能力）。
- `RepeatCondition`：重复条件（为空默认满足，重复次数>0时仅在条件满足时继续重复）。

### `Effect`
运行时效果请求，包含目标、输出与连锁能力等信息。
- 位置：`Server/Domain/GameFlow/EffectQueue/Effect.cs`
- 关键字段：
  - `SelectedTargets`：本次效果执行的目标列表
  - `TriggeredEventId`：触发本效果的事件 id
  - `SourceCardId`：源卡牌实例 id（可空）
  - `ChainedAbilities`：效果执行后需要立即处理的连锁能力
  - `GeneratedEvents`：效果执行生成的事件（包含 `EffectCompletedEvent`）

### `GameActionTransactionService`
事务处理引擎：按 `Before/On/Commit/After` 生命周期处理事件，并管理全局效果队列。
- 位置：`Server/Services/GameActionTransactionService.cs`

### `EffectExecutor`
执行单个 `Effect`：解析参数 → 调用 handler → 解析 Outputs → 追加 `EffectCompletedEvent`。
- 位置：`Server/Domain/Abilities/Effects/EffectExecutor.cs`

## Effect Handler 注册与扩展

当前实现不再使用 `IGameStateActionDispatcher` 之类的“字符串分发器”，而是通过扫描程序集自动注册 handler：
- 注册中心：`Server/Domain/Abilities/Effects/Handlers/Services/EffectHandlerRegistry.cs`
- 约定：
  - handler 实现 `IEffectHandler<TParams>`
  - handler 标注 `[EffectHandler(EffectType.Xxx)]`
  - `EffectExecutor` 会自动解析 `EffectConfig.Parameters` 到 `TParams` 并执行

## Outputs 与连锁能力（ChainedAbilities）

- Outputs：在 handler 生成事件后，`EffectExecutor` 会用 `IValueSourceResolver` 计算 `EffectConfig.Outputs` 并生成 `EffectCompletedEvent`（见 `Server/Domain/Events/SignalEvents.cs`）。
- 连锁能力：`Effect` 执行完成后，事务引擎会基于 `effect.EffectConfig.ChainedAbilities` 模板实例化能力，并以 `EffectCompletedEvent` 作为连锁能力的 `TriggeringEvent`（见 `Server/Services/GameActionTransactionService.cs`）。

## 常见 DSL 访问点（建议）

- 需要读取“本次效果目标”：`context.selectedTargets`
- 需要读取“能力目标池”：`context.resolvedTargets`
- 需要读取“效果输出”：`context.triggeringEvent.outputs`（连锁能力触发事件为 `EffectCompletedEvent` 时）
- 需要读取“效果生成事件”：`context.triggeringEvent.generatedEvents`
- 需要读取“上一个效果的输入/目标”：`context.triggeringEvent.originalEffectInputParams` / `context.triggeringEvent.resolvedTargets`
