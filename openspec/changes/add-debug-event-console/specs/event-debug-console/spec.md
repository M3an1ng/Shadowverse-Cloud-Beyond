## ADDED Requirements
### Requirement: 调试控制台可见性与入口
调试控制台 MUST 仅在对战界面且 DEBUG 模式下显示，并提供输入与输出日志区域；非 DEBUG 或非对战页面 MUST 隐藏。

#### Scenario: DEBUG 对战界面显示控制台
- **WHEN** 客户端进入对战界面且开启 DEBUG 标志
- **THEN** 调试控制台入口可见并可展开输入命令

#### Scenario: 非 DEBUG 场景隐藏控制台
- **WHEN** 客户端未开启 DEBUG 或处于非对战页面
- **THEN** 控制台入口不可见，无法触发调试命令

### Requirement: 调试指令语法与校验
控制台 MUST 支持用户友好的指令语法，默认格式为 `command key=value ...`，值可用引号包裹；未通过校验的指令 MUST 仅回显错误且不改动游戏状态。指令集至少包含：
- `help`：显示可用指令与示例。
- `status [player=p1|p2]`：查询指定玩家或双方的核心状态摘要。
- `damage target=<cardId> amount=<int> [source=<cardId|p1|p2>]`
- `heal target=<cardId> amount=<int> [source=<cardId|p1|p2>]`
- `kill target=<cardId>` / `destroy target=<cardId>` / `vanish target=<cardId>` / `return target=<cardId>`
- `draw player=<p1|p2> count=<int>`
- `mana player=<p1|p2> delta=<int>`（PP/EP 等资源调整，按当前模型校验）
- `add-card player=<p1|p2> card=<cardDefId> location=hand|board`
- `event name=<EventType> payload=<json>`（兜底原始事件注入，用于高级调试）

#### Scenario: 合法指令被解析并回显成功
- **WHEN** 用户输入 `damage target=card-123 amount=3 source=p1`
- **THEN** 控制台解析出命令及参数，并返回“已受理”回执

#### Scenario: 非法指令被拦截
- **WHEN** 用户输入缺少必需参数的指令（如 `damage amount=3`）
- **THEN** 控制台返回错误消息并不修改任何游戏状态

### Requirement: 事件指令映射与日志回显
控制台命令集 MUST 覆盖当前 `Server/Domain/Events` 下的事件（含 ActionRequests、SignalEvents），并为后台事件广播提供同样的指令格式回显。至少提供以下映射（指令名 → 事件/用途 → 示例参数），同时允许 `event name=<EventType> payload=<json>` 作为兜底：
- 卡牌生命周期/移动：`play-card`→PlayCard/PlayCardRequested，`summon`→Summon，`add-hand`→AddCardToHand，`discard`→CardDiscarded，`move`→CardMoved，`destroy`→Destroy，`vanish`→CardVanished，`return`→Return/CardReturnedToHand，`transform`→Transform，`burn`→CardBurned。
- 战斗与伤害：`attack`→Attack/AttackDeclared/Clash，`damage`→Damage，`heal`→Heal，`stat`→StatChanged/HealthChanged（可选参数 atk= hp=），`attack-state`→AttackStateUpdated。
- 资源：`pp`→PpUpdate，`pp-max`→PpMaxUpdate，`ep`→EpUpdate，`ep-max`→EpMaxUpdate。
- 进化：`evolve`→Evolve/EvolveRequested，`super-evolve`→SuperEvolve，`evolve-state`→EvolveStateUpdated。
- 回合/对局：`turn-start`→TurnStart，`turn-end`→TurnEnd/EndTurnRequested。
- 抽牌与牌库：`draw`→Draw，`deck-count`→DeckCountChanged，`deck-empty`→DeckEmptyOnDraw，`graveyard`→GraveyardUpdated。
- 关键字/状态：`buff-add`→BuffApplied，`buff-remove`→BuffRemoved，`keyword-add`→KeywordAdded，`keyword-remove`→KeywordRemoved，`silence`→SilenceApplied，`silence-clear`→SilenceRemoved，`countdown-finish`→CountdownFinished。
- 动作/提交：`play-request`→PlayCardRequested，`attack-request`→AttackRequested，`evolve-request`→EvolveRequested，`endturn-request`→EndTurnRequested，`mulligan-submit`→MulliganSubmitted，`mulligan-done`→MulliganCompleted，`choice`→PlayerChoiceSubmitted，`effect-done`→EffectCompleted，`ability`→AbilityTriggered，`hand-update`→HandUpdated。

#### Scenario: 日志事件被转换为指令格式
- **WHEN** 后端广播 `DamageEvent`（来源 p1 卡，目标 card-123，amount=3）
- **THEN** 前台日志以 `damage target=card-123 amount=3 source=p1` 形式呈现

#### Scenario: 新事件使用兜底格式
- **WHEN** 收到未在映射表中的事件类型
- **THEN** 日志与控制台使用 `event name=<EventType> payload=<json>` 兜底展示/重放，且不阻塞现有映射

### Requirement: 调试指令触发事件管线
被接受的调试指令 MUST 映射为暂存事件并通过 GameService 的事务性/可录制事件管线（参考 15/16 设计）提交：先生成暂存事件或事件包，允许响应阶段校验/修正，最终调用 `Commit` 或 `CommitAction` 完成状态修改并广播事件 DTO 与最新状态。执行结果 MUST 回显在控制台。
调试指令 MUST 直接生成现有的游戏事件类型（如 `CardMovedEvent`、`DamageEvent` 等），不得引入仅用于调试且与现有事件行为重复的 `Debug*Event` 包装事件。

#### Scenario: 调试抽牌指令驱动事件提交
- **WHEN** 用户输入 `draw player=p1 count=2` 且校验通过
- **THEN** 系统生成对应的暂存事件包，调用事件管线提交，两张牌加入 p1 手牌并广播更新，同时控制台显示“执行成功”

#### Scenario: 调试击杀指令走提交与广播
- **WHEN** 用户输入 `kill target=card-456` 且目标存在
- **THEN** 系统生成击杀相关的可提交事件，完成提交并广播死亡事件/状态变化，控制台显示成功回执

#### Scenario: 调试指令不新增重复事件类型
- **WHEN** 为现有事件能力新增/扩展调试指令（如 `vanish`、`return`、`add-card`）
- **THEN** 系统直接使用现有事件类型表达意图并提交，不新增与其行为等价的 `Debug*Event` 类型
