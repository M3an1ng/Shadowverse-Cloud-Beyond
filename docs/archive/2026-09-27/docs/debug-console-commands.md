# 调试控制台指令手册 (DEBUG 模式)

前提：URL `?debug=1` 或 `localStorage.svb.debug=true`，仅在对战界面启用。命令格式 `command key=value ...`，值可用引号包裹。

## 基础
- `help`：显示帮助
- `clear`：清空输出
- `connection`：检查 SignalR 连接
- `gamestate` / `status [player=p1|p2]`：状态摘要
- `monitor on|off`：开启/关闭事件监听

## 事件/状态指令
- 伤害/治疗：`damage target=<cardId> amount=<int> [source=<cardId|p1|p2>]`；`heal target=<cardId> amount=<int> [source=...]`
- 击杀/移除：`destroy target=<cardId> [source=...]`；`vanish target=<cardId>`；`return target=<cardId>`
- 抽牌：`draw player=<p1|p2> count=<int>`
- 资源：`mana player=<p1|p2> delta=<int>`（当前 PP 调整）；`pp player=<p1|p2> current=<int> [max=<int>]`；`ep player=<p1|p2> current=<int> [max=<int>]`
- 加卡：`add-card player=<p1|p2> card=<cardDefId> location=hand|board`
- 兜底事件：`event name=<EventType> payload=<json>`（未来扩展）

## 动作类兼容指令（旧接口）
- `play <cardUid> [targetUid]`；`attack <attackerUid> <targetUid>`；`evolve <unitUid>`；`endturn`

## 日志映射示例
后端事件广播会映射为可读指令格式：
- DamageEvent → `damage target=<uid> amount=<n> source=<uid|p1|p2>`
- DestroyEvent → `destroy target=<uid> [source=...]`
- DrawEvent/DeckUpdate → `draw player=<pid> count=<n>` 或 `deck-count player=<pid> count=<n>`
- Pp/Ep 更新 → `pp player=<pid> current=<n> [max=<n>]` / `ep ...`
- TurnStart/TurnEnd → `turn-start player=<pid> [turn=n]` / `turn-end ...`
- 进化 → `evolve target=<uid> [player=...]`
- HandUpdated → `hand-update player=<pid> [count=n]`
- 未知事件 → `event name=<EventType> payload=<json>`

## 使用示例
- 对手主战者打 3 点：`damage target=<leader-uid> amount=3 source=p1`
- 让先手抽 2 张：`draw player=p1 count=2`
- 提升 PP：`pp player=p1 current=5 max=6` 或 `mana player=p1 delta=+2`
- 生成卡到手牌：`add-card player=p2 card=2001 location=hand`

## 快速回归步骤（手动）
1) 非 DEBUG 环境：控制台不显示。
2) DEBUG 环境：控制台可见，输入 `help` 正常输出。
3) 指令校验：输入 `damage amount=3` → 返回错误，不改状态。
4) 合法指令：输入 `damage target=<有效UID> amount=1` → 状态变更并日志回显为指令格式。
5) 事件日志：触发一次伤害/抽牌/PP 更新，日志中显示对应指令文本。
