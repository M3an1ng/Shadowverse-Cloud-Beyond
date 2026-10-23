# 卡牌动画反馈调查 - 操作表现矩阵

日期：2026-06-29

## 反馈层定义

- primary motion：卡牌、攻击者、投射物或目标的主体运动。
- impact flash：命中帧闪光。
- shape accent：斩击、圆环、冲击波、能量线。
- target reaction：目标后仰、震动、缩放、闪白、暗化。
- screen feedback：棋盘震动、局部震动、全屏闪。
- residuals：余光、烟尘、碎片、残影。

## 出牌

当前状态：`handleCardPlayed` 仅记录使用历史，`CardMoved` 负责区域变更。

推荐表现：

- hand lift：手牌先抬起并放大，80-120ms。
- travel：飞到中央施法区或场地目标 slot，160-260ms。
- commit flash：卡牌短暂亮边，80-140ms。
- resolution：随从落场或法术释放。

最低标准：

- 玩家必须能看到是哪张牌被打出。
- 扣费、离手、入场不能是静默跳变。

实现建议：

- 使用 transient clone 表示打出的卡，避免原手牌被 `CardMoved` 先移除。
- `CardPlayed` 作为视觉起点，`CardMoved` 作为 authoritative state。

## 召唤 / 入场

当前状态：`handleCardSummoned` 是视觉 placeholder，集合 enter 动画会处理新卡。

推荐表现：

- slot glow：目标位置先亮起，80-120ms。
- card drop：卡牌从上方或中心落入，160-220ms。
- settle：轻微压缩/回弹，120-180ms。
- optional burst：召唤来源不同可加不同颜色。

最低标准：

- 新单位出现时必须有落点和身份可读性。

## 攻击

当前状态：`handleAttackDeclared` 有明确钩子但未播放动画。

推荐表现：

- windup：攻击者后撤或抬起，80-140ms。
- travel：向目标方向冲刺，120-200ms。
- hit-stop：命中瞬间停顿，40-70ms。
- impact：目标闪光和冲击环，120-200ms。
- recovery：攻击者回位，140-220ms。

最低标准：

- 必须能看清攻击者、目标和命中帧。
- 攻击主战者和攻击随从应有不同强度。

实现建议：

- 优先用 attacker clone 做冲刺，避免 `.selected-attacker` transform 冲突。
- `AttackDeclared` 播放 windup/travel，`DamageDealt` 播放 impact/number/target reaction。

## 受击 / 伤害

当前状态：已有强化后的 `.damage-number`，但目标没有反应。

推荐表现：

- target recoil：目标沿攻击方向或随机短震，180-320ms。
- flash：卡面/头像闪白或闪红，80-140ms。
- damage number：保持当前 1.25s 可读数字。
- heavy damage：大于阈值时加局部 shake。

最低标准：

- 伤害数字、目标反应、血量变化至少三者有两项同步出现。

实现建议：

- 新增 `showHitReaction(targetUid, options)`。
- 对场上单位加 class，对主战者头像加 class。
- 对即将死亡的单位使用 overlay clone，防止节点被移除导致动画截断。

## 治疗

当前状态：`showHealNumber` 有绿色数字，目标无恢复反馈。

推荐表现：

- green pulse：目标绿色脉冲 220-360ms。
- rising number：保留治疗数字。
- health sparkle：少量向上粒子或光点。

最低标准：

- 治疗应和伤害在颜色、音调、运动方向上明显区分。

## 能力触发

当前状态：`showAbilityTriggerToast` 能显示文字，但来源/目标关系不明确。

推荐表现：

- source focus：来源卡牌抬起或发光，120-200ms。
- title/toast：能力名称显示，当前已有基础。
- link/payload：能量线或投射物指向目标，120-260ms。
- target resolution：目标反馈。

最低标准：

- 玩家必须能识别“哪张卡触发了能力”。

实现建议：

- `AbilityTriggered` 只负责来源聚焦和 toast。
- 具体目标反馈由后续 `DamageDealt`、`HealApplied`、`StatChanged`、`CardMoved` 等事实事件驱动。
- 对无目标能力使用来源环形 burst。

## 法术

当前状态：没有独立法术释放表现，通常由 CardPlayed 和后续事实事件表现。

推荐表现：

- card reveal：法术卡在中央短暂展示。
- cast burst：按类型显示颜色。
- payload：单体法术有投射线，AOE 有场地波纹。
- resolution：目标各自播放反馈。

最低标准：

- 法术牌不应只从手牌消失。

## 破坏 / 消失 / 返回

当前状态：`UnitDestroyed`、`CardVanished`、`CardReturned` 都是 placeholder，实际移动由 `CardMoved` 承担。

推荐表现：

- destroy：暗化、裂光、碎散或向墓地飞出。
- vanish：紫/蓝色溶解或收束消失。
- return：向手牌方向飞回。
- tuck/deck：向牌库方向飞回并缩小。

最低标准：

- 不同移动原因应有不同视觉语义，至少 destroy 和 vanish 不应相同。

## 进化 / 超进化

当前状态：替换卡牌状态，超进化有持续边缘光。

推荐表现：

- focus：场面压暗，目标卡聚焦，120-180ms。
- burst：金色/蓝色能量爆发，220-360ms。
- transform：卡面替换或翻面，220-360ms。
- stat punch：攻击/生命变化弹出。

最低标准：

- 进化是高价值动作，必须高于普通出牌和普通攻击的视觉优先级。

## Reveal / Fusion

当前状态：已有较完整实现。

下一步增强：

- Reveal：加入来源说明和队列优先级，避免打断关键战斗命中。
- Fusion：本地素材飞入已有，补充目标主卡聚焦、融合后卡牌状态脉冲。

## 行动优先级

P0：

- 攻击者冲刺与回位。
- 目标受击反应。
- 能力来源聚焦。
- 出牌 clone 飞行动画。

P1：

- 破坏/消失/返回差异化。
- 进化瞬间表现。
- 法术中心展示和投射。
- 主战者受击强化。

P2：

- AOE 波纹。
- 屏幕 shake 分级。
- 粒子残留。
- 稀有能力专属特效。

