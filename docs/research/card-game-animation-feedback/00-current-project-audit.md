# 卡牌动画反馈调查 - 当前项目审计

日期：2026-06-29

## 结论

当前项目已经具备较完整的战斗事件流和局部动画基础，但缺少“动作级动画编排”。也就是说，服务器已经能告诉客户端发生了什么，客户端也有 DOM/CSS 动画能力；短板在于没有把 `AttackDeclared`、`DamageDealt`、`AbilityTriggered`、`UnitDestroyed` 等事件编排成接近商业卡牌游戏的动作链。

## 已有基础

- SignalR 事件接入完整：`CardPlayed`、`CardSummoned`、`AttackDeclared`、`DamageDealt`、`HealApplied`、`UnitDestroyed`、`UnitEvolved`、`CardRevealed`、`Fusion`、`AbilityTriggered` 等都在 `src/main.js` 中 wire 到 `gameManager`。
- `src/ui/cardAnimationSystem.js` 已有卡牌集合动画系统，支持 enter、move、exit 三类动画，并且使用 transient clone 处理离场 overlay。
- `src/ui.js` 已有 Reveal 队列、Fusion 飞卡、AbilityTrigger toast、伤害/治疗数字。
- `src/styles/game.css` 已有卡牌进出场、拖拽目标线、Reveal overlay、Fusion burst、伤害数字动画。
- `prefers-reduced-motion` 已在部分卡牌进出场动画中被考虑，可以延续为战斗动效的无障碍策略。

## 关键缺口

### 出牌

`handleCardPlayed` 当前主要记录使用过的卡牌，注释标记为 “Placeholder for play-card visual effects”。`CardMoved` 会改变区域并触发集合动画，但缺少商业级出牌常见的“手牌抬起 -> 飞向施法/场地 -> 放大确认 -> 结算”的专用动作。

### 召唤

`handleCardSummoned` 当前保留给视觉效果，但没有实际 summon burst、落地冲击、场地 slot 高亮或召唤来源表达。

### 攻击

`handleAttackDeclared` 当前只校验 attacker/target，并有 “Future: Trigger attack animation start here?” 注释。攻击者没有 windup、冲刺、命中停顿、回位，目标也没有受击反应。

### 受击/伤害

`handleDamageDealt` 当前职责是显示伤害数字。伤害数字已加强，但目标卡牌/主战者本身不抖动、不闪白、不后仰，缺少命中帧。

### 破坏/消失/返回

`handleUnitDestroyed`、`handleCardVanished`、`handleCardReturned` 都是 placeholder 或仅刷新预览。实际离场依赖 `CardMoved` 的集合 exit 动画，缺少 fatal impact、破碎、吸收、放逐等原因差异化表现。

### 能力触发

`handleAbilityTriggered` 会显示 toast，但没有绑定来源卡牌高亮、来源到目标的能量线、目标反应或结算顺序。因此玩家能看到“能力触发了”，但不一定能看清“谁影响了谁”。

### 进化/超进化

`handleUnitEvolved` 会替换单位并更新 UI。已有 `super-evolved` 持续边缘光效，但缺少进化瞬间的聚焦、爆发、卡面替换过渡和数值变化强调。

### Fusion / Reveal

Fusion 和 Reveal 是当前相对完整的视觉模块。Fusion 对本地玩家会 clone 手牌并飞向中心，Reveal 有队列与遮罩。但两者还可以进一步接入统一动作队列，避免和其他战斗事件互相抢焦点。

## 当前实现风险

- `updateUnitDisplay` 会重建整张场上卡牌 DOM，直接挂在原节点上的动画 class 可能被替换打断。
- `.selected-attacker` 使用 transform，攻击动画如果直接叠加 transform 会互相覆盖。
- `CardMoved` 可能在 `DamageDealt` 或 `UnitDestroyed` 周围到达，目标节点可能先被替换或移除，需要 transient clone 保护关键帧。
- 大量事件连发时，如果每个事件都立即播放完整动画，会造成排队过长和信息噪音。

## 建议架构方向

新增一个轻量 `combatFeedback` 层，放在 `src/ui` 附近：

- 只接收事件级输入：attackerUid、targetUid、amount、reason、sourceCardUid。
- 使用 DOM/CSS + transient overlay 实现首批效果。
- 对原始卡牌节点只做短期 class；关键动作使用 clone 或 overlay，避免被状态重渲染打断。
- 支持动作等级：minor、normal、major、fatal。
- 支持 reduced motion：缩短位移、保留颜色/数字/高亮。

