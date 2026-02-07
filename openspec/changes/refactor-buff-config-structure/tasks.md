## 1. Implementation
- [x] 1.1 定义 BuffConfig 结构（含唯一标识与基础字段）。
- [x] 1.2 在 AuraConfig 中改为保存 BuffConfigs 列表。
- [x] 1.3 实现 BuffTemplateConfig -> BuffConfig 的适配层（保持当前执行流程）。
- [x] 1.4 更新 AuraManager 使用适配层生成多 Buff（不再合并）。
- [ ] 1.5 更新序列化/编辑器元数据以支持 BuffConfig 列表。

## 2. Tests
- [ ] 2.1 现有 Aura 刷新用例无行为变化。
- [ ] 2.2 BuffConfig 适配层从旧模板生成等价 BuffConfig。
