# Change: Add Choose/Fusion/Act/Reveal interactive abilities

## Why
- 当前后端 `EffectType` 已定义但未实现的关键交互能力（`Choose/Fusion/Act/Reveal`）会阻塞“影之诗式”卡牌设计与对局体验。
- 参考实现中这些能力高度依赖“玩家在手牌/选项上的选择交互”（见参考：`info/reference1/Game/Effect/EffectAbilityHandler.cs`），而现系统仅完成了单选目标的基础链路，缺少：
  - 多选（带确认/最小最大数量）；
  - 选项选择（非卡牌/非玩家实体的选择，例如“从两种能力中选一种发动”）；
  - 场上“启动（Act）”的客户端入口与每回合一次的约束；
  - “公开（Reveal）/融合（Fusion）”的前端表现与事件契约。

## What Changes
- **后端：新增 4 个交互能力的端到端能力链**
  - `Choose`：从多种能力分支中选择一种执行（以配置中的分支能力为候选）。
  - `Fusion`：从手牌选择 1..N 张卡作为“融合素材”，并按规则离开手牌后继续后续效果。
  - `Act`：场上卡牌的“启动型能力”，每回合最多发动一次；提供服务端验证与触发事件。
  - `Reveal`：公开指定卡牌（通常是隐藏信息区域中的卡牌），并广播给双方客户端用于视觉表现与日志/回放。
- **后端：扩展玩家选择系统（通用卡牌选择能力）**
  - 支持“目标多选 + 确认”（min/max，取消、超限、非法提交的校验）。
  - 支持“选项选择”（非实体目标），用于 `Choose` 分支选择。
  - 支持“跨区域的卡牌选择”（手牌/战场/墓地/牌堆等）：当目标不在战场可直接点击时，服务端 MUST 提供足够的信息让客户端渲染一个可选择列表/面板。
- **后端：补齐 effect 目标选择器能力**
  - 实现 `EffectConfig.TargetSelector` 的执行能力，用于让 `Fusion/Reveal` 等效果在同一能力包含多目标组时精准取用目标（当前为 TODO 且返回空列表）。
- **前端：新增交互 UI 与特效**
  - 通用卡牌多选 UI（覆盖手牌/战场/墓地/牌堆等；高亮可选、已选计数、确认/取消）。
  - 场上卡牌“Act”按钮（可用/不可用状态、点击发起请求）。
  - `Fusion` 素材卡“被融合离开手牌”的表现。
  - `Reveal` 的翻牌/高亮/展示浮层等表现，并保证双方一致可见。

## Non-Goals
- 不在本变更中实现 `Accelerate/Crystalize/Discover/SpellBoost` 等其它未实现效果。
- 不在本变更中定义完整的卡牌脚本 DSL 语法扩展（如需新增 DSL 关键字，将作为后续独立变更）。
- 不在本变更中引入新的前端框架；UI 仍基于现有 `src/ui.js` 的原生渲染体系。

## Impact
- Backend (planned):
  - `Server/Domain/Abilities/Ability.cs`（Choose 分支解析 / TargetSelector 执行）
  - `Server/Domain/Abilities/Targets/*`（玩家选择：多选/选项）
  - `Server/Domain/Abilities/Effects/Handlers/*`（Fusion/Reveal/Act 相关处理器）
  - `Server/Domain/Events/*`、`Server/Dtos/SignalR/*`（Reveal/Fusion/Act 的事件与 DTO）
  - `Server/Hubs/GameHub.cs`（Act 请求入口；沿用现有 out-of-band choice 提交流）
- Frontend (planned):
  - `src/ui.js`（多选/选项选择 UI、融合/公开特效）
  - `src/gameManager.js`（新增事件处理与状态更新；Act 请求）
- Tests (planned):
  - `Server.Tests`：选择合法性、Fusion 素材处理、Act 每回合一次、Reveal 广播与回放字段完整性。

## Open Questions (for review)
- `Reveal` 的可见性边界：默认“公开给双方”，是否需要支持“仅对自己公开”（例如检索/抽牌预览）？
- `Fusion` 素材去向：默认移出游戏（不进入墓地）；实现上可能需要新增“移出游戏”区域或补充卡牌移动原因。
- `Act` 的冷却粒度：是“每张卡每回合一次”，还是“同名能力每回合一次”，以及是否受沉默/变形影响？
- “牌堆选择”的信息公开边界：若允许从牌堆中选择卡牌，服务端需要先提供“候选清单”（检索/发现式呈现），而不是让客户端直接点击未知的牌堆。
