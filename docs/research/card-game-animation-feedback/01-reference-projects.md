# 卡牌动画反馈调查 - 参考项目与资料

日期：2026-06-29

## 商业级参考

### Hearthstone

参考价值最高。Hearthstone 的关键启发不是单纯“特效多”，而是所有操作都有触觉化反馈：卡牌会抬起、倾斜、飞行，攻击有跳跃和命中，法术有投射路径，较大伤害会带来棋盘震动。Wired 对 Hearthstone 制作采访提到，团队把“epic”理解为 craftsmanship，而不是单纯规模；文章还举例说明随从攻击会跳向目标，闪电法术会沿空间释放，大量伤害会让场面震动。

适合作为本项目 P0/P1 反馈标准：

- 每个动作必须能被玩家感知。
- 数值变化不能静默发生。
- 轻动作短，重动作有额外命中层。
- 棋盘/卡牌应像实体物件一样有重量。

资料：

- https://www.wired.com/2014/04/hearthstone
- https://gdcvault.com/play/1022036/Hearthstone-How-to-Create-an
- https://www.gamedeveloper.com/design/video-designing-an-immersive-user-interface-for-i-hearthstone-i-

### Shadowverse / Shadowverse: Worlds Beyond

主题最接近。调查重点应放在：

- 进化/超进化的聚焦和爆发。
- 攻击时卡牌/角色视觉中心的处理。
- 能力触发时来源卡、目标、文字提示的关系。
- 主战者受击和胜负瞬间表现。

商业参考结论：Shadowverse 类表现更偏“卡面+角色特效”，本项目短期不能复制复杂 3D/Live2D，但可以复制事件节奏：来源聚焦、目标反馈、结算明确。

### Legends of Runeterra

适合参考“技能/法术队列”和“响应窗口”的清晰表达。Runeterra 的 stack 区域本质上是视觉化未结算行动，适合本项目未来处理复杂连锁能力。

资料：

- https://www.reddit.com/r/LegendsOfRuneterra/comments/ewr8jq/stack_interaction_in_legends_of_runeterra/
- https://cjleo.com/blog/legends-of-runeterra-and-player-agency-in-collectible-card-games/

### MTG Arena

适合参考目标线、触发队列、法术结算顺序和卡牌 hover 详情。其表现不一定最“爽”，但复杂规则可读性强。

### Marvel Snap

适合参考快速短局的反馈密度：翻面、揭示、地点触发、短爆发、快速回到可操作状态。对本项目的启发是：不必每个动作都长，但每个动作都要有明确焦点。

## 开源项目

### OpenDuelyst

地址：https://github.com/open-duelyst/duelyst

价值：

- 完整开源的商业卡牌/战棋混合项目。
- GitHub README 表示它是 Duelyst 的源码，客户端使用 Backbone.js + Marionette，渲染引擎为 Cocos2d-JS。
- 适合调查完整游戏如何组织资源、动画、战斗事件、客户端场景。

限制：

- 技术栈和本项目 DOM/CSS/Vite 不同。
- 更适合参考架构和资源组织，不适合直接移植代码。

### hearthstone-web

地址：https://github.com/Rymedy/hearthstone-web

价值：

- JS 浏览器 Hearthstone clone。
- 可参考浏览器内卡牌拖拽、目标选择、基础动画组织。

限制：

- fan clone 的视觉质量和工程成熟度通常低于商业项目。

### Hearthstone Clone App

地址：https://github.com/EnginKARATAS/hearthstone-clone-game

价值：

- React/CSS/Tailwind 方向，和现代 Web 前端更接近。
- 可参考状态驱动 UI、响应式布局和卡牌战斗结构。

限制：

- 需要实测动画细节，不能只看 README 判断质量。

### cards.js / deck-of-cards

地址：

- https://einaregilsson.github.io/cards.js/
- https://github.com/deck-of-cards/deck-of-cards

价值：

- 专注卡牌渲染、移动、洗牌、发牌和动画 API。
- 对本项目的“手牌飞向场地”“牌库抽牌”“卡牌回手”很有参考价值。

限制：

- 偏传统扑克牌，缺少 CCG 战斗反馈。

### hearthbreaker

地址：https://github.com/danielyule/hearthbreaker

价值：

- 规则模拟参考，尤其是 Hearthstone 行为细节。

限制：

- 不是 UI 动画参考。

## 参考结论

短期最值得学习的是 Hearthstone 的反馈原则、Runeterra 的结算清晰度、cards.js 的 Web 卡牌移动 API、OpenDuelyst 的完整游戏组织。不要从 fan clone 直接复制视觉标准；它们更适合作为实现方式和代码结构参考。

