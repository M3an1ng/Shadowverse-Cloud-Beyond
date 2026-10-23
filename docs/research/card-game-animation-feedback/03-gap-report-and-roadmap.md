# 卡牌动画反馈调查 - 差距报告与路线图

日期：2026-06-29

## 商业级反馈标准

本项目的目标不应是立刻复制炉石或影之诗的复杂特效资源，而是达到接近商业卡牌游戏的反馈清晰度：

- 每个重要操作都有起点、焦点、结算。
- 玩家能看清“谁做了什么、影响了谁、结果是什么”。
- 轻动作短，重动作有额外反馈，致命动作有终结感。
- 状态更新不能静默跳变。
- 动画不应长期阻塞操作，也不应遮挡卡牌文本和关键数字。

## 当前差距

### 最高风险差距

1. 攻击没有动作链  
`AttackDeclared` 没有动画，`DamageDealt` 只有数字。攻击行为缺少商业卡牌游戏最核心的冲击反馈。

2. 能力触发只有 toast  
来源和目标之间缺少视觉连接，复杂连锁时玩家难以理解事件关系。

3. 出牌缺少确认瞬间  
`CardMoved` 可以让集合变化，但不是“这张牌被打出”的动作表现。

4. 破坏/消失/返回视觉语义不区分  
当前主要依赖移动和离场，缺少 destroy、vanish、return 的差异化。

### 中等风险差距

- 进化/超进化缺少瞬间表现。
- 主战者受击弱于卡牌受击。
- Fusion/Reveal 已有，但还没有统一优先级队列。
- 动画没有完整行动队列，连发事件可能互相打断。

## 实施路线

### Phase 1：战斗命中核心反馈

目标：让攻击和受击达到最低商业可读标准。

任务：

- 新增 `showAttackMotion(attackerUid, targetUid)`。
- 新增 `showHitReaction(targetUid, options)`。
- 新增 `showImpactBurst(targetUid, options)`。
- `handleAttackDeclared` 调用攻击者动作。
- `handleDamageDealt` 调用目标受击、冲击 burst、伤害数字。
- 对主战者头像和场上卡牌分别处理。

验收：

- 攻击时能看清攻击者、目标和命中帧。
- 目标受击不依赖伤害数字也能被注意到。
- 目标死亡时动画不会被 DOM 移除截断。

### Phase 2：出牌与召唤反馈

目标：让卡牌从手牌到场地/施法区的过程可见。

任务：

- 新增 `showPlayCardMotion(cardUid, ownerId, cardDto)`。
- 使用 transient clone 从手牌飞向中央或场地 slot。
- `CardSummoned` 补召唤落点 glow 和 settle。
- 法术牌显示短暂 center cast card。

验收：

- 玩家能明确看到哪张牌被打出。
- 随从召唤和法术释放视觉语义不同。

### Phase 3：能力触发链路

目标：让触发能力从“文字通知”升级为“来源 -> 目标 -> 结果”的链路。

任务：

- `AbilityTriggered` 来源卡牌 focus。
- ability toast 与来源卡绑定位置或颜色。
- 后续事实事件接 target reaction。
- 对无目标能力显示来源环形 burst。

验收：

- 多个能力连发时仍能看清当前来源。
- 玩家能分辨主动使用能力、入场能力、死亡能力、光环类能力。

### Phase 4：高价值动作表现

目标：增强进化、破坏、消失、Fusion、Reveal 等高记忆点。

任务：

- 进化/超进化聚焦和爆发。
- destroy/vanish/return/tuck 不同离场视觉。
- Fusion 主卡吸收反馈。
- Reveal 队列优先级调整。

验收：

- 高价值动作有高于普通动作的层级。
- 离场原因能从动画风格中被区分。

## 推荐技术路线

短期：

- DOM clone + CSS keyframes。
- Web Animations API 用于需要动态位移的 attacker/target 运动。
- 保留现有 `cardAnimationSystem` 处理集合 enter/move/exit。

中期：

- 轻量 `combatFeedbackQueue`，控制能力触发、攻击、伤害、死亡的播放顺序。
- 为每个 action 定义 duration budget。

暂不建议：

- 立刻上 WebGL/Three.js。
- 大量粒子系统。
- 为每张卡做专属动画。

## 第一批实现建议

建议从 Phase 1 开始，原因是它直接解决玩家最频繁感知到的弱反馈：

1. `DamageDealt` 增加目标抖动/闪光。
2. `DamageDealt` 增加 impact burst。
3. `AttackDeclared` 增加攻击者 clone 冲刺。
4. 对致命伤害预留 fatal class，但先不做复杂死亡特效。

这批可以保持 KISS：不改服务器事件，不引入新依赖，不碰规则逻辑，只新增 UI 动画函数和 CSS。

