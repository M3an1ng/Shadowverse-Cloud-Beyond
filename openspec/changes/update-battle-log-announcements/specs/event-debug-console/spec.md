## MODIFIED Requirements
### Requirement: 事件指令映射与日志回显
控制台命令集 MUST 覆盖当前 `Server/Domain/Events` 下的事件（含 ActionRequests、SignalEvents），并为调试控制台提供指令格式回显；Battle Log 展示 MUST 使用公告接口提供的中文可读文本。至少提供以下映射（指令名 → 事件/用途 → 示例参数），同时允许 `event name=<EventType> payload=<json>` 作为兜底：
- 卡牌生命周期/移动：`play-card`→PlayCard/PlayCardRequested，`summon`→Summon，`add-hand`→AddCardToHand，`discard`→CardDiscarded，`move`→CardMoved，`destroy`→Destroy，`vanish`→CardVanished，`return`→Return/CardReturnedToHand，`transform`→Transform，`burn`→CardBurned。
- 战斗与伤害：`attack`→Attack/AttackDeclared/Clash，`damage`→Damage，`heal`→Heal，`stat`→StatChanged/HealthChanged（可选参数 atk= hp=），`attack-state`→AttackStateUpdated。
- 资源：`pp`→PpUpdate，`pp-max`→PpMaxUpdate，`ep`→EpUpdate，`ep-max`→EpMaxUpdate。
- 进化：`evolve`→Evolve/EvolveRequested，`super-evolve`→SuperEvolve，`evolve-state`→EvolveStateUpdated。
- 回合/对局：`turn-start`→TurnStart，`turn-end`→TurnEnd/EndTurnRequested。
- 抽牌与牌库：`draw`→Draw，`deck-count`→DeckCountChanged，`deck-empty`→DeckEmptyOnDraw，`graveyard`→GraveyardUpdated。
- 关键字/状态：`buff-add`→BuffApplied，`buff-remove`→BuffRemoved，`keyword-add`→KeywordAdded，`keyword-remove`→KeywordRemoved，`silence`→SilenceApplied，`silence-clear`→SilenceRemoved，`countdown-finish`→CountdownFinished。
- 动作/提交：`play-request`→PlayCardRequested，`attack-request`→AttackRequested，`evolve-request`→EvolveRequested，`endturn-request`→EndTurnRequested，`mulligan-submit`→MulliganSubmitted，`mulligan-done`→MulliganCompleted，`choice`→PlayerChoiceSubmitted，`effect-done`→EffectCompleted，`ability`→AbilityTriggered，`hand-update`→HandUpdated。

#### Scenario: 日志事件展示为用户友好文本
- **WHEN** 后端广播 `DamageEvent`（来源 p1 卡，目标 card-123，amount=3）
- **THEN** 前台 Battle Log 以中文可读文本呈现，并突出关键变量

#### Scenario: 调试指令回显使用指令格式
- **WHEN** 用户输入 `damage target=card-123 amount=3 source=p1`
- **THEN** 调试控制台回显仍为指令格式，便于复制/重放

#### Scenario: 新事件使用兜底格式
- **WHEN** 收到未在映射表中的事件类型
- **THEN** 日志与控制台使用 `event name=<EventType> payload=<json>` 兜底展示/重放，且不阻塞现有映射