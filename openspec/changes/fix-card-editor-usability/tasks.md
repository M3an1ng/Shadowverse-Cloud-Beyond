## 1. Specification
- [ ] 1.1 明确卡牌编辑器 MVP 验收路径（打开→选择→编辑→保存/另存为→删除）
- [ ] 1.2 明确能力编辑/关联验收路径（列表→创建→编辑→关联/解除→刷新回显）
- [ ] 1.3 明确枚举与本地化的契约（大小写/值域/前端归一化策略）

## 2. Backend (API + Persistence)
- [x] 2.1 将 `POST /api/abilities` 从“模拟响应”改为真实落库并返回创建结果
- [x] 2.2 `GET /api/cards/{cardId}/abilities` 返回真实能力数据（非占位），并包含编辑器所需字段（含 `jsonConfig`）
- [x] 2.3 增加卡牌能力关联端点：`POST /api/cards/{cardId}/abilities/{abilityId}`（关联）
- [x] 2.4 增加卡牌能力解除端点：`DELETE /api/cards/{cardId}/abilities/{abilityId}`（解除）
- [x] 2.5（可选）增加 `DELETE /api/cards/{id}` 以支持删除卡牌
- [x] 2.6 补齐最小集成测试（Abilities CRUD、Cards<->Abilities 关联、cards 列表/详情）

## 3. Frontend (Card Editor)
- [x] 3.1 绑定 `SAVE / SAVE AS / DELETE` 按钮事件并对接后端 API
- [x] 3.2 实现 `NEW ENTRY`（创建新卡牌）最小可用（进入编辑态 + 另存为/创建）
- [x] 3.3 修复明显运行时错误：去除/替换不存在的 `loadCardAbilities()` 调用，改为刷新能力列表
- [x] 3.4 修复关键字枚举加载：统一 `MetadataManager` 方法名与调用方（`CardKeyword`）
- [x] 3.5 实现搜索/筛选逻辑（`card-search`, `class-filter`, `type-filter`, `rarity-filter`）
- [x] 3.6 增加枚举值归一化（例如 `Follower`/`follower`）以保证 UI 判断与回显一致

## 4. Manual Verification
- [ ] 4.1 本地启动后端 + Vite 前端，走通卡牌编辑器 MVP 全流程
- [ ] 4.2 验证能力编辑器创建/编辑/关联/解除全流程（含刷新回显）
