## ADDED Requirements
### Requirement: 统一事件公告接口
事件日志输出 MUST 依赖统一接口提供的公告文本；仅实现该接口的事件可被广播到前端日志。接口需支持基于事件实例生成可读公告，禁止在管线中使用手写 switch 映射。

#### Scenario: 公告事件生成日志文本
- **WHEN** 一个实现公告接口的事件被广播
- **THEN** 系统调用其公告方法生成文本，并发送到前端日志 UI

#### Scenario: 非公告事件不进入日志
- **WHEN** 事件未实现公告接口（如 HandPlayabilityUpdateDto）
- **THEN** 前端日志不显示该事件

### Requirement: 调试指令解析接口
所有 `ICommittableEvent` MUST 提供统一的调试指令解析入口，负责校验参数并返回“成功/错误 + 事件列表或消息”。调试入口不得绕过此接口直接入管线。

#### Scenario: 指令缺少必需参数时报错
- **WHEN** 用户输入 `damage` 且缺少 target/amount
- **THEN** 解析接口返回错误消息，事件不入管线

#### Scenario: 合法指令生成事件
- **WHEN** 用户输入合法指令（如 `damage target=x amount=3`）
- **THEN** 解析接口返回成功及对应事件列表进入事务管线

### Requirement: 日志渲染与映射取消手工 switch
日志渲染 MUST 使用事件接口提供的公告文本，不得通过硬编码 switch 手动映射命令文本；调试指令日志映射也应复用同一接口或解析结果。

#### Scenario: 日志文本来自事件接口
- **WHEN** 日志显示一条事件记录
- **THEN** 文本来源于事件公告接口输出，而非手写 switch 映射

#### Scenario: 调试指令日志使用统一入口
- **WHEN** 调试指令执行成功或失败
- **THEN** 日志/回显文本来自统一的解析接口结果
