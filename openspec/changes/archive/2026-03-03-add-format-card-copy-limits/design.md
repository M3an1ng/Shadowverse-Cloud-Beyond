## Context

当前卡组限张规则在后端 `DeckValidationService` 和前端 `deckBuilder` 中均以固定值 `3` 实现，策略调整需要改代码和重新发布。该限制还影响默认卡组自动生成与对局前校验，一旦需要按环境/规则做禁卡或限 1/2，现有架构无法快速落地。

本变更需要在不破坏既有数据与接口兼容的前提下，引入格式维度（`DeckEnvironment + DeckRule`）下的单卡动态限张策略，并保证“配置、校验、提示、默认卡组生成”使用同一规则源。

约束：
- 现有环境默认为 `Open`，规则包含 `Standard`/`DualClass`；
- 旧数据中没有禁限配置时必须保持默认 `3` 的行为；
- 现有保存/导入/开局校验路径已集中依赖 `DeckValidationService`，应尽量复用。

## Goals / Non-Goals

**Goals:**
- 建立按 `(Environment, Rule, CardDefinitionId)` 生效的禁限策略模型，支持 `0..3`，`0` 表示禁用。
- 将后端所有卡组入口统一切到动态限张校验，替换固定 `3` 判断。
- 提供策略管理接口（查询、批量 upsert、删除回退默认值）。
- 前端 warning/保存错误提示与后端原因保持一致（支持多条原因展示）。
- 默认卡组生成遵守策略，避免生成即违规。

**Non-Goals:**
- 不改动战斗运行时效果逻辑。
- 不引入按用户、按段位、按活动的个性化禁限。
- 不在本次设计中新增环境或规则枚举类型。

## Decisions

### Decision 1: 新增独立策略表，不改 `CardDefinition` 主表结构
- Choice: 新增实体 `DeckCardCopyLimitPolicy`（命名可微调），字段包含 `Environment`、`Rule`、`CardDefinitionId`、`MaxCopies`、时间戳，并对三元组建立唯一索引。
- Rationale: 禁限属于“格式策略”而非卡牌静态属性，独立表更利于动态维护和审计。
- Alternatives:
  - 把限张写入 `CardDefinition`：无法表达不同环境/规则差异，否决。
  - 配置 JSON 文件：运行时难事务化更新与查询，否决。

### Decision 2: 限张值范围固定 `0..3`，缺省回退 `3`
- Choice: 接口和校验层统一校验 `MaxCopies` in `[0,3]`；缺失策略时用默认上限 `3`。
- Rationale: 与当前游戏规则兼容，迁移风险最低；`0` 可表达禁卡。
- Alternatives:
  - 允许大于 3：会引入与核心规则冲突，不在本次范围。

### Decision 3: 以后端校验服务为单一事实源
- Choice: 在 `DeckValidationService` 内解析有效限张并校验，不在 controller/其他 service 重复实现。
- Rationale: 避免逻辑分叉，确保创建、更新、导入、开局一致。
- Alternatives:
  - 前后端各自实现完整规则：漂移风险高，否决。

### Decision 4: 前端只做同源提示，不定义新规则
- Choice: 前端通过策略查询接口获取当前 format 的限张映射，仅用于 warning/提示；最终合法性以后端返回为准。
- Rationale: 降低前端维护成本，保证一致性。
- Alternatives:
  - 前端硬编码规则：与需求冲突，否决。

### Decision 5: 默认卡组生成接入同一限张解析
- Choice: 默认卡组构建阶段按每卡有效上限分配数量；当总可用容量不足 40 时显式失败。
- Rationale: 避免 bootstrap 产出无效卡组。
- Alternatives:
  - 继续固定按 3 生成后再校验：会导致“创建即失败/不可用”，体验差，否决。

## Risks / Trade-offs

- [Risk] 策略接口被非管理员误用导致大面积不可对战。
  - Mitigation: 复用现有 admin actor 权限校验模式，仅管理员可写。

- [Risk] 限张策略查询引入额外数据库访问。
  - Mitigation: 在 `DeckValidationService` 按 `(environment, rule)` 批量加载并在单次校验中复用；必要时加短期缓存。

- [Risk] 错误提示文本与前端多语言/既有文案不一致。
  - Mitigation: 统一错误模板，前端直接展示后端主错误原因，并保留多条合并逻辑。

- [Trade-off] 新增一张策略表和 API，短期复杂度上升。
  - Benefit: 后续平衡调整无需改代码发布，收益显著。

## Migration Plan

1. **Schema**
- 新增策略实体与 EF 配置。
- 新增迁移：创建策略表 + 唯一索引 + 必要索引。

2. **Backend**
- 新增策略查询/管理服务与控制器（GET/PUT/DELETE，支持批量 upsert）。
- `DeckValidationService` 替换固定 `3` 检查为动态限张检查。
- 默认卡组构建逻辑接入动态限张上限。

3. **Frontend**
- DeckBuilder 在当前 `(environment, rule)` 下加载策略并更新 warning 展示。
- 保存失败路径显示后端返回的完整限张原因，多错误换行。

4. **Verification**
- 覆盖测试：默认回退、限 0/1/2/3、导入/保存/开局一致性、默认卡组可构建性。
- 手工验证：设置禁卡后 UI warning、保存报错、对战可用性状态一致。

### Rollback Strategy
- 迁移为新增表，不改动核心旧表字段；回滚应用后旧逻辑仍可工作。
- 如需回退策略影响，可清空策略表（非 destructive schema rollback）。

## Open Questions

- Devtools 对战实验室是否允许可选绕过禁限（例如 `IgnoreCardCopyLimits`）？默认建议不绕过。
- 策略管理接口是挂在 devtools 专用路由还是通用 admin API？
- 错误文案是否需要统一国际化键值而非直接文本？
