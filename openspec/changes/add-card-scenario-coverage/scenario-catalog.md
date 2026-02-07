# Card Scenario Catalog

This catalog lists a suggested executable scenario for each converted card. Use it to drive per-card scenario tests.

## abysscraft

### 1050402 蝙蝠
- Type: Follower, Cost: 1
- Ability keywords: none
- Trigger events: none
- Effect types: none
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.

### 1050403 怨灵
- Type: Follower, Cost: 1
- Ability keywords: none
- Trigger events: Destroy, Return, Tuck, TurnEnd
- Effect types: ReplaceEvent, Vanish
- Scenario steps:
  - Destroy the card after it enters the field.
  - Return the card to hand to trigger leave effects.
  - Tuck the card into deck to trigger leave effects.
  - Advance to turn end with the card on the field.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: ReplaceEvent, Vanish.
- Notes: Before-intercept: ensure event rewrite occurs

### 1250402 骸骨士兵
- Type: Follower, Cost: 0
- Ability keywords: none
- Trigger events: none
- Effect types: none
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.

### 1451301 灵魂转化
- Type: Spell, Cost: 1
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: Destroy, Draw
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to move to graveyard after resolution.
  - Assert effects resolve: Destroy, Draw.

### 1451302 呼魂女巫
- Type: Follower, Cost: 4
- Ability keywords: EvolveEffect, Necromance
- Trigger events: Evolve, SuperEvolve
- Effect types: Necromancy, Summon
- Scenario steps:
  - Evolve the card after it enters the field.
  - Super-evolve the card after it enters the field.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Necromancy, Summon.
- Notes: Requires token definitions available in data

### 1451303 魅惑的魅魔·莉莉姆
- Type: Follower, Cost: 1
- Ability keywords: Lastword
- Trigger events: Destroy
- Effect types: GetToken
- Scenario steps:
  - Destroy the card after it enters the field.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: GetToken.

### 1451402 禁约掘墓人
- Type: Follower, Cost: 2
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: Damage, GetToken
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Damage, GetToken.

### 1451403 迷魂归途
- Type: Spell, Cost: 2
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: Reanimate
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to move to graveyard after resolution.
  - Assert effects resolve: Reanimate.

### 1451404 恶毒的小木乃伊
- Type: Follower, Cost: 2
- Ability keywords: Necromance
- Trigger events: PlayCard
- Effect types: Necromancy
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Necromancy.

### 1451405 狂猎恶魔
- Type: Follower, Cost: 6
- Ability keywords: Sacrifice
- Trigger events: PlayCard
- Effect types: Sacrifice
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Sacrifice.

## dragoncraft

### 1240402 炽炎幼龙
- Type: Follower, Cost: 1
- Ability keywords: none
- Trigger events: none
- Effect types: none
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.

### 1340401 大海虎鲸
- Type: Follower, Cost: 2
- Ability keywords: none
- Trigger events: none
- Effect types: none
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.

### 1440401 飞龙
- Type: Follower, Cost: 1
- Ability keywords: none
- Trigger events: none
- Effect types: none
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.

### 1441301 新手龙骑士
- Type: Follower, Cost: 3
- Ability keywords: Enhance, Fanfare
- Trigger events: PlayCard
- Effect types: Summon
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Summon.
- Notes: Requires token definitions available in data

### 1441302 龙之灵视
- Type: Spell, Cost: 3
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: Choose
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to move to graveyard after resolution.
  - Assert effects resolve: Choose.
- Notes: Requires deterministic choice setup

### 1441303 猛击的龙族勇士
- Type: Follower, Cost: 4
- Ability keywords: EvolveEffect
- Trigger events: Evolve, SuperEvolve
- Effect types: Damage
- Scenario steps:
  - Evolve the card after it enters the field.
  - Super-evolve the card after it enters the field.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Damage.

### 1441401 龙人碎击
- Type: Spell, Cost: 1
- Ability keywords: Awake, Fanfare
- Trigger events: PlayCard
- Effect types: Damage
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to move to graveyard after resolution.
  - Assert effects resolve: Damage.

### 1441402 烈焰火蜥蜴
- Type: Follower, Cost: 2
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: Damage
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Damage.

### 1441403 潜泳纵队
- Type: Spell, Cost: 5
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: Summon
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to move to graveyard after resolution.
  - Assert effects resolve: Summon.
- Notes: Requires token definitions available in data

### 1441404 凶鲨战士
- Type: Follower, Cost: 8
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: Damage
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Damage.

## forestcraft

### 1010402 妖精
- Type: Follower, Cost: 1
- Ability keywords: none
- Trigger events: none
- Effect types: none
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.

### 1210402 森林的奥秘
- Type: Spell, Cost: 0
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: Heal
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to move to graveyard after resolution.
  - Assert effects resolve: Heal.

### 1411301 旋律的演奏者
- Type: Follower, Cost: 4
- Ability keywords: EvolveEffect
- Trigger events: Evolve, SuperEvolve
- Effect types: SetKeyword, Summon
- Scenario steps:
  - Evolve the card after it enters the field.
  - Super-evolve the card after it enters the field.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: SetKeyword, Summon.
- Notes: Requires token definitions available in data

### 1411302 流水庭院
- Type: Amulet, Cost: 2
- Ability keywords: none
- Trigger events: Act, Summon
- Effect types: Heal, SetCountdown, Summon
- Scenario steps:
  - Use Act on the card.
  - Expect the card to remain on the field unless countdown reaches 0.
  - Assert effects resolve: Heal, SetCountdown, Summon.
- Notes: Requires token definitions available in data

### 1411303 冒险精灵·小梅
- Type: Follower, Cost: 1
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: Damage
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Damage.

### 1411401 自由牧者
- Type: Follower, Cost: 1
- Ability keywords: Enhance, Fanfare
- Trigger events: PlayCard
- Effect types: SetStatus
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: SetStatus.

### 1411402 林中旅程
- Type: Spell, Cost: 2
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: Draw, GetToken
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to move to graveyard after resolution.
  - Assert effects resolve: Draw, GetToken.

### 1411403 妖精召集令
- Type: Spell, Cost: 1
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: GetToken
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to move to graveyard after resolution.
  - Assert effects resolve: GetToken.

### 1411404 大自然的警告
- Type: Amulet, Cost: 2
- Ability keywords: none
- Trigger events: Act
- Effect types: Choose, Destroy
- Scenario steps:
  - Use Act on the card.
  - Expect the card to remain on the field unless countdown reaches 0.
  - Assert effects resolve: Choose, Destroy.
- Notes: Requires deterministic choice setup

## havencraft

### 1060401 神圣猎鹰
- Type: Follower, Cost: 3
- Ability keywords: none
- Trigger events: none
- Effect types: none
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.

### 1060402 壮丽大神隼
- Type: Follower, Cost: 6
- Ability keywords: none
- Trigger events: none
- Effect types: none
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.

### 1461301 持矛锤的僧侣
- Type: Follower, Cost: 4
- Ability keywords: EvolveEffect
- Trigger events: Evolve, SuperEvolve
- Effect types: Vanish
- Scenario steps:
  - Evolve the card after it enters the field.
  - Super-evolve the card after it enters the field.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Vanish.

### 1461303 白羽石像
- Type: Amulet, Cost: 1
- Ability keywords: Lastword
- Trigger events: Act, Destroy
- Effect types: SetCountdown, Summon
- Scenario steps:
  - Use Act on the card.
  - Destroy the card after it enters the field.
  - Expect the card to remain on the field unless countdown reaches 0.
  - Assert effects resolve: SetCountdown, Summon.
- Notes: Requires token definitions available in data

### 1461304 亚巴顿
- Type: Follower, Cost: 9
- Ability keywords: AttackEffect
- Trigger events: Attack
- Effect types: Destroy
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Destroy.

### 1461401 圣枪战士
- Type: Follower, Cost: 4
- Ability keywords: EvolveEffect, Fanfare
- Trigger events: Evolve, PlayCard
- Effect types: SetStatus
- Scenario steps:
  - Evolve the card after it enters the field.
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: SetStatus.

### 1461402 投影鸟像
- Type: Amulet, Cost: 4
- Ability keywords: Lastword
- Trigger events: Act, Destroy
- Effect types: SetCountdown, Summon
- Scenario steps:
  - Use Act on the card.
  - Destroy the card after it enters the field.
  - Expect the card to remain on the field unless countdown reaches 0.
  - Assert effects resolve: SetCountdown, Summon.
- Notes: Requires token definitions available in data

### 1461403 受祝勇士
- Type: Follower, Cost: 2
- Ability keywords: none
- Trigger events: none
- Effect types: none
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.

### 1461404 清泉修士
- Type: Follower, Cost: 5
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: Heal
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Heal.

## neutral

### 1401301 治愈的旋律
- Type: Spell, Cost: 3
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: Draw, Heal
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to move to graveyard after resolution.
  - Assert effects resolve: Draw, Heal.

### 1401302 商队猛犸象
- Type: Follower, Cost: 7
- Ability keywords: none
- Trigger events: none
- Effect types: none
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.

### 1401303 哥布林杀手·路西乌斯&汀娜
- Type: Follower, Cost: 4
- Ability keywords: EvolveEffect
- Trigger events: Evolve, SuperEvolve
- Effect types: Destroy
- Scenario steps:
  - Evolve the card after it enters the field.
  - Super-evolve the card after it enters the field.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Destroy.

### 1401401 侦探的放大镜
- Type: Amulet, Cost: 2
- Ability keywords: none
- Trigger events: Act
- Effect types: Destroy, SetKeyword
- Scenario steps:
  - Use Act on the card.
  - Expect the card to remain on the field unless countdown reaches 0.
  - Assert effects resolve: Destroy, SetKeyword.

### 1401402 叮当天使·莉亚
- Type: Follower, Cost: 2
- Ability keywords: EvolveEffect, Lastword
- Trigger events: Destroy, Evolve
- Effect types: Draw
- Scenario steps:
  - Destroy the card after it enters the field.
  - Evolve the card after it enters the field.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Draw.

### 1401403 激震的歌利亚
- Type: Follower, Cost: 4
- Ability keywords: none
- Trigger events: none
- Effect types: none
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.

### 1401404 不屈的剑斗士
- Type: Follower, Cost: 2
- Ability keywords: Enhance
- Trigger events: PlayCard
- Effect types: SetStatus
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: SetStatus.

## portalcraft

### 1070402 改良型·悬丝傀儡
- Type: Follower, Cost: 1
- Ability keywords: none
- Trigger events: TurnEnd
- Effect types: Destroy
- Scenario steps:
  - Advance to turn end with the card on the field.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Destroy.

### 1070405 悬丝傀儡
- Type: Follower, Cost: 0
- Ability keywords: none
- Trigger events: TurnEnd
- Effect types: Destroy
- Scenario steps:
  - Advance to turn end with the card on the field.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Destroy.

### 1470301 纹章:加速引擎
- Type: Crest, Cost: 0
- Ability keywords: none
- Trigger events: Summon
- Effect types: SetKeyword
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain in the emblem zone.
  - Assert effects resolve: SetKeyword.

### 1470401 古老的创造物
- Type: Follower, Cost: 0
- Ability keywords: none
- Trigger events: none
- Effect types: none
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.

### 1470402 解析的创造物
- Type: Follower, Cost: 0
- Ability keywords: Lastword
- Trigger events: Destroy
- Effect types: Draw
- Scenario steps:
  - Destroy the card after it enters the field.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Draw.

### 1470403 公转的创造物
- Type: Follower, Cost: 0
- Ability keywords: none
- Trigger events: none
- Effect types: none
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.

### 1471302 人偶剧场
- Type: Amulet, Cost: 2
- Ability keywords: Fanfare
- Trigger events: PlayCard, TurnEnd
- Effect types: GetToken
- Scenario steps:
  - Play the card in normal mode.
  - Advance to turn end with the card on the field.
  - Expect the card to remain on the field unless countdown reaches 0.
  - Assert effects resolve: GetToken.

### 1471303 加速引擎
- Type: Amulet, Cost: 1
- Ability keywords: none
- Trigger events: Act
- Effect types: AddEmblem, Destroy
- Scenario steps:
  - Use Act on the card.
  - Expect the card to remain on the field unless countdown reaches 0.
  - Assert effects resolve: AddEmblem, Destroy.

### 1471304 战地机械师
- Type: Follower, Cost: 4
- Ability keywords: EvolveEffect
- Trigger events: Evolve, SuperEvolve
- Effect types: GetToken
- Scenario steps:
  - Evolve the card after it enters the field.
  - Super-evolve the card after it enters the field.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: GetToken.

### 1471401 创造物防壁
- Type: Spell, Cost: 6
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: GetToken
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to move to graveyard after resolution.
  - Assert effects resolve: GetToken.

### 1471402 调频分析员
- Type: Follower, Cost: 2
- Ability keywords: Enhance
- Trigger events: AddCardToHand, PlayCard
- Effect types: GetToken, SetStatus
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: GetToken, SetStatus.

### 1471403 人偶长矛手
- Type: Follower, Cost: 2
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: GetToken
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: GetToken.

### 1471404 镜像刻录者
- Type: Follower, Cost: 3
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: GetToken
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: GetToken.

## runecraft

### 1030401 大地之魔片
- Type: Amulet, Cost: 1
- Ability keywords: none
- Trigger events: Act
- Effect types: AddEarthSigil
- Scenario steps:
  - Use Act on the card.
  - Expect the card to remain on the field unless countdown reaches 0.
  - Assert effects resolve: AddEarthSigil.

### 1030403 泥尘巨像
- Type: Follower, Cost: 1
- Ability keywords: none
- Trigger events: none
- Effect types: none
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.

### 1030404 守护者巨像
- Type: Follower, Cost: 3
- Ability keywords: none
- Trigger events: none
- Effect types: none
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.

### 1431301 辉彩元素使
- Type: Follower, Cost: 4
- Ability keywords: EvolveEffect
- Trigger events: Evolve, SuperEvolve
- Effect types: AddEarthSigil, Damage, EarthRite
- Scenario steps:
  - Evolve the card after it enters the field.
  - Super-evolve the card after it enters the field.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: AddEarthSigil, Damage, EarthRite.

### 1431302 魔焰毁灭者
- Type: Follower, Cost: 10
- Ability keywords: SpellBoost
- Trigger events: SpellBoost
- Effect types: SetStatus
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: SetStatus.

### 1431303 流水药剂师
- Type: Follower, Cost: 2
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: Choose
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Choose.
- Notes: Requires deterministic choice setup

### 1431401 魔女的炼金炉
- Type: Amulet, Cost: 1
- Ability keywords: Fanfare
- Trigger events: Act, PlayCard
- Effect types: AddEarthSigil, Draw
- Scenario steps:
  - Use Act on the card.
  - Play the card in normal mode.
  - Expect the card to remain on the field unless countdown reaches 0.
  - Assert effects resolve: AddEarthSigil, Draw.

### 1431402 智慧光辉
- Type: Spell, Cost: 1
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: Draw
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to move to graveyard after resolution.
  - Assert effects resolve: Draw.

### 1431403 疾风爆裂
- Type: Spell, Cost: 1
- Ability keywords: Fanfare, SpellBoost
- Trigger events: PlayCard, SpellBoost
- Effect types: Damage
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to move to graveyard after resolution.
  - Assert effects resolve: Damage.

### 1431404 尘土化形
- Type: Spell, Cost: 2
- Ability keywords: EarthRitual, Fanfare
- Trigger events: PlayCard
- Effect types: EarthRite, Summon
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to move to graveyard after resolution.
  - Assert effects resolve: EarthRite, Summon.
- Notes: Requires token definitions available in data

## swordcraft

### 1020401 骑士
- Type: Follower, Cost: 0
- Ability keywords: none
- Trigger events: none
- Effect types: none
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.

### 1421301 见习骑士
- Type: Follower, Cost: 2
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: Evolve
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Evolve.

### 1421303 历战近卫
- Type: Follower, Cost: 4
- Ability keywords: EvolveEffect
- Trigger events: Evolve, SuperEvolve
- Effect types: SetKeyword, SetStatus, Summon
- Scenario steps:
  - Evolve the card after it enters the field.
  - Super-evolve the card after it enters the field.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: SetKeyword, SetStatus, Summon.
- Notes: Requires token definitions available in data

### 1421304 陷阵骑将
- Type: Follower, Cost: 8
- Ability keywords: AttackEffect
- Trigger events: Attack
- Effect types: SetAttackTimes, SetKeyword
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: SetAttackTimes, SetKeyword.

### 1421401 须臾剑士
- Type: Follower, Cost: 1
- Ability keywords: none
- Trigger events: none
- Effect types: none
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.

### 1421402 动员令
- Type: Spell, Cost: 2
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: GetToken
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to move to graveyard after resolution.
  - Assert effects resolve: GetToken.

### 1421404 月夜暗杀者
- Type: Follower, Cost: 4
- Ability keywords: Enhance
- Trigger events: PlayCard
- Effect types: Destroy
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to remain on the field unless removed by effects.
  - Assert effects resolve: Destroy.

### 1421405 军备锻造
- Type: Spell, Cost: 3
- Ability keywords: Fanfare
- Trigger events: PlayCard
- Effect types: SetKeyword, SetStatus
- Scenario steps:
  - Play the card in normal mode.
  - Expect the card to move to graveyard after resolution.
  - Assert effects resolve: SetKeyword, SetStatus.

