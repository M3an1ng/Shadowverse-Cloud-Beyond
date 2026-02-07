# Change: 引入 BuffConfig 并保持现有执行流程

## Why
当前 AuraConfig 依赖 BuffTemplateConfig 作为单一复合 Buff 模型，不利于后续拆分为多 Buff 事件的执行流程。先升级数据结构为 BuffConfig（可生成 Buff 的独立配置单元），并保持现有执行链路不变，可为后续流程调整做准备，同时避免当前行为回归。

## What Changes
- 新增 BuffConfig 作为独立数据结构，用于描述单个 Buff 的生成规则。
- AuraConfig 从 BuffTemplateConfig 迁移为 BuffConfigs 列表，运行期不再合并 Buff，而是逐个 BuffConfig 生成 Buff（前端按 EffectKey 自动合并显示）。
- 为未来“逐个 BuffConfig 生成事件”预留稳定的 BuffConfig 标识与 effectKey 规则。

## Impact
- Specs: persistent-auras
- Code: AuraConfig / BuffTemplateConfig / BuffFactory / AuraManager / Buff serialization
