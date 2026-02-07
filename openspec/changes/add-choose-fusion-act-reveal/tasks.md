## 0. Discovery (Baseline)
- [ ] 0.1 盘点当前 `EffectType.Choose/Fusion/Act/Reveal` 的实现状态（Handlers/Events/UI）。
- [ ] 0.2 复核玩家选择链路：`TargetSelectionService` → `PendingActionService` → `GameHub.SubmitTargetChoice` → 事务恢复点。
- [ ] 0.3 确认 `EffectConfig.TargetSelector` 现状与缺口（`Server/Domain/Abilities/Ability.cs` 当前 TODO 返回空列表）。
- [ ] 0.4 明确前端现有选择 UI 能力（当前默认基于战场点击）与需要补齐的“跨区域选择面板”（手牌/墓地/牌堆候选清单等）。

## 1. Specification
- [ ] 1.1 明确 `Choose/Fusion/Act/Reveal` 的最小行为边界（交互、取消、失败策略、可见性）。
- [ ] 1.2 定义 `Choose` 的分支数据来源（建议：`EffectConfig.ChainedAbilities`）与 UI 展示字段（Name/Description）。
- [ ] 1.3 定义“通用卡牌选择”可覆盖的区域与展示策略（战场直接点击、手牌直接点击、其他区域使用面板候选列表）。
- [ ] 1.4 定义 `Fusion` 素材选择范围（己方手牌）与去向（默认移出游戏）以及输出（fusedCount/fusedIds）。
- [ ] 1.5 定义 `Act` 的可用性规则与“每回合一次”的状态持久位置（CardInstance/Ability runtime）。
- [ ] 1.6 定义 `Reveal` 的广播范围（默认双方）与事件负载（InstanceId + CardDefinition 简要信息）。
- [ ] 1.7 定义 `Act` 请求/提交事件分层与费用/条件判定（PreConditions + EnhanceCost）。

## 2. Backend: Player Choice System
- [x] 2.1 支持多选：客户端提交多个 id，服务端校验 min/max 与集合包含关系。
- [x] 2.2 为选择请求增加“模式/元数据”以支持非目标实体的 `Choose` 选项（最小可用即可，采用 CardZone.Extra 临时区条目 + SelectableCardInstances）。
- [x] 2.3 AI：为多选与选项选择提供决策实现（可先用启发式/随机作为最小版本）。

## 3. Backend: TargetSelector Execution
- [x] 3.1 实现 `EffectConfig.TargetSelector` 的执行器（从 `ResolvedTargets` 中取子集），并接入 `Ability.CreateEffectsAsync`。
- [x] 3.2 增加测试：TargetSelector 不为空时不应返回空列表（覆盖至少一个 target group）。

## 4. Backend: Effects & Events
- [x] 4.1 `FusionEffectHandler`：结算素材离开手牌并产生领域事件（移动/消失/移出游戏等），并输出素材信息供后续链使用。
- [x] 4.2 `RevealEffectHandler`：产生公开事件并广播 DTO（供前端动画与日志）。
- [x] 4.3 `Act`：新增请求事件/Hub 方法（例如 `RequestAct`），服务端验证与执行 Activated 能力，并广播状态更新。
- [ ] 4.7 `Act`：拆分请求事件与可提交事件，费用与条件在提交前验证并扣除。
- [x] 4.4 `Choose`：在效果/能力生成阶段完成分支选择（不阻塞同步 handler），并只执行选中分支。

## 5. Frontend: UI/UX & Effects
- [x] 5.1 多选 UI（通用）：可选目标高亮、点击切换选择、显示计数、确认/取消按钮、超限提示。
- [x] 5.2 选择面板（跨区域）：当可选目标来自墓地/牌堆候选清单等不常驻区域时，展示一个可滚动的卡牌列表供选择与确认。
- [x] 5.3 选项选择 UI：以“临时区（CardZone.Extra）条目 + 通用选择面板”呈现 `Choose` 分支（标题、描述、确认/取消）。
- [x] 5.4 Act 按钮：场上可启动卡牌渲染按钮，点击发送请求；不可用时禁用/隐藏。
- [x] 5.5 Reveal 动画：公开卡牌翻面/高亮展示，并支持多卡连续展示。
- [x] 5.6 Fusion 动画：被融合卡牌从手牌“飞入/消失”并伴随日志提示。

## 6. Tests
- [x] 6.1 多选合法性：不足/超限/非法 id 必须被拒绝且不污染状态。
- [x] 6.2 Choose 分支：选择 A 只执行 A，不执行 B；取消按策略处理。
- [x] 6.3 Fusion：素材从手牌移除，移出游戏（或指定去向），并可被后续链读取 outputs。
- [x] 6.4 Act：同一卡牌同回合第二次 Act 必须被拒绝；回合切换后恢复可用。
- [ ] 6.7 Act：费用不足或条件不满足时请求被拒绝且不消耗 Act。
- [x] 6.5 Reveal：对局双方均收到公开事件；回放/事件日志包含公开信息。
- [x] 6.6 跨区域选择：当选择目标来自墓地/牌堆候选清单时，客户端仍可完成选择并被服务端校验通过。

## 7. Validation
- [x] 7.1 `dotnet test`（Server.Tests）
- [x] 7.2 `openspec validate add-choose-fusion-act-reveal --strict`
