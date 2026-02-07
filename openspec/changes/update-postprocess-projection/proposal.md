# Change: Update Post-Processing to Property Projection

## Why
当前后处理阶段只能转换为 CardDefinition/Instance DTO，无法满足“取 self 某属性列表”的需求。

## What Changes
- 后处理阶段从“固定 DTO 转换”改为“按 self 项属性/表达式投影”。
- **BREAKING**：移除/废弃 `ToCardDefinitionDto` / `ToCardInstanceDto` 语义与配置入口。
- 前端管线编辑器支持配置后处理表达式（例如 `self.CurrentCost`）。
- 新增管线阶段 `select`，用于对集合执行投影（可与后处理配合使用）。

## Impact
- 受影响的 specs: query-pipeline
 - 受影响的代码: Server/Domain/Query (PostProcessingOperation/SelectOperation), Server/Domain/Query/QueryExecutor, 前端 DSL pipeline editor
