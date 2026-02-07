# Change: Allow DSL Input for All Effect Parameters

## Why
当前只有部分参数支持 DSL 输入，导致编辑体验不一致，且高级用户需要手动绕过结构化输入。

## What Changes
- 为所有参数提供 DSL 输入模式，并与结构化输入互斥
- 参数元数据增加 DSL 能力声明（AllowDsl/DslType/DslMode）
- 优化参数 UI 展示与可读性（不包含 DSL 性能优化）

## Impact
- 受影响的 specs: effect-parameter-input
- 受影响的代码: 事件/效果参数元数据服务、能力编辑器 UI、DSL 校验
