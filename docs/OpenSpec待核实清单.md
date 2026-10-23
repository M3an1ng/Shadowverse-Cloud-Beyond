# OpenSpec 待核实清单

> 2026-09-27 快照。任务数来自当前 tasks.md；统计不是实现验收、测试报告或自动归档决定。

## 盘点结果

- 未归档变更目录：163。
- 任务全勾选：81；含未勾选项：81；缺任务文件：1。
- 当前主规范文件：4。
- 多个变更涉及同一 capability 的情况：33 组。

本次保留所有 changes/specs 的内容、勾选和位置。未选定并逐项验收的变更不批量同步或归档，避免旧增量覆盖较新的需求。

## 已核对的具体边界

| 变更 | 当前证据 | 保留原因 |
|---|---|---|
| [房间/单局 ID 分离](../openspec/changes/add-roomid-and-keep-gameid-per-match/tasks.md) | [MatchController](../Server/Controllers/MatchController.cs) 已有 RoomId/ActiveGameId 及兼容字段；[GameHub](../Server/Hubs/GameHub.cs) 同时读取新旧参数 | tasks 仍含删除兼容分支和多局/重连验证项，未按目录存在即归档 |
| [对局表情](../openspec/changes/add-in-match-emote-system/tasks.md) | [EmoteCatalogService](../Server/Services/EmoteCatalogService.cs)、[前端目录](../src/emoteCatalog.js) 已存在 | tasks 仍含聊天链路测试等未勾选项；本次局部测试不等于验收全部场景 |
| [活动任务](../openspec/changes/add-activity-quest-system/tasks.md) | [QuestProgressService](../Server/Services/QuestProgressService.cs) 有周期与来源去重；奖励领取在经济服务 | 源码存在不等于所有迁移、人工与集成验证完成，状态按下表保留 |
| [回放日志](../openspec/changes/add-replay-event-journal/tasks.md) | [ReplayController](../Server/Controllers/ReplayController.cs) 存在，但 [ReplayOptions](../Server/Options/ReplayOptions.cs) 默认关闭 Journal 写入 | 验收应覆盖实际配置、数据写入和读取，不沿用旧运维说明 |
| [实例连续性](../openspec/changes/refactor-card-instance-continuity-version/proposal.md) | 目前该变更仅提供提案，没有 tasks 文件 | 不以相近代码或旧战斗连续性字段代替本提案验收 |

## 下一步核验标准

1. 先选定变更并读取 proposal、design、tasks、delta specs；跨变更冲突先梳理替代关系。
2. 每条需求映射到实际实现与测试；把测试存在、测试已运行、人工验收分开记录。
3. 对比主规范后再同步增量，最后按项目 OpenSpec 技能归档；保留原 .openspec.yaml。
4. “主规范缺对应项”只表示未发现同名主规范文件，不表示实现缺失或内容从未被其他规范覆盖。

## 全量清单

| 变更 | 已勾选/总任务 | 待核实状态 | 增量规范数 | 主规范缺对应项 |
|---|---:|---|---:|---:|
| [add-ability-action-handles](../openspec/changes/add-ability-action-handles/) | 5/15 | 有未勾选项 | 1 | 1 |
| [add-ability-editor-disable-toggles](../openspec/changes/add-ability-editor-disable-toggles/) | 6/7 | 有未勾选项 | 1 | 1 |
| [add-action-restriction-buffs](../openspec/changes/add-action-restriction-buffs/) | 0/5 | 有未勾选项 | 1 | 1 |
| [add-activity-quest-system](../openspec/changes/add-activity-quest-system/) | 41/42 | 有未勾选项 | 2 | 2 |
| [add-admin-reset-password](../openspec/changes/add-admin-reset-password/) | 3/4 | 有未勾选项 | 1 | 1 |
| [add-alternate-play-modes](../openspec/changes/add-alternate-play-modes/) | 21/45 | 有未勾选项 | 1 | 1 |
| [add-battle-card-preview](../openspec/changes/add-battle-card-preview/) | 2/18 | 有未勾选项 | 1 | 1 |
| [add-before-event-intercept-effects](../openspec/changes/add-before-event-intercept-effects/) | 6/8 | 有未勾选项 | 1 | 1 |
| [add-buff-conditional-removal](../openspec/changes/add-buff-conditional-removal/) | 0/5 | 有未勾选项 | 1 | 1 |
| [add-buff-turn-owner-duration-types](../openspec/changes/add-buff-turn-owner-duration-types/) | 4/4 | 任务全勾选 | 1 | 1 |
| [add-card-art-assets](../openspec/changes/add-card-art-assets/) | 7/7 | 任务全勾选 | 1 | 1 |
| [add-card-code-versioning](../openspec/changes/add-card-code-versioning/) | 0/9 | 有未勾选项 | 1 | 1 |
| [add-card-community-rewards](../openspec/changes/add-card-community-rewards/) | 14/15 | 有未勾选项 | 1 | 1 |
| [add-card-countdown-field](../openspec/changes/add-card-countdown-field/) | 6/6 | 任务全勾选 | 1 | 1 |
| [add-card-effect-instance-mode](../openspec/changes/add-card-effect-instance-mode/) | 5/5 | 任务全勾选 | 1 | 1 |
| [add-card-instance-updated-event](../openspec/changes/add-card-instance-updated-event/) | 4/4 | 任务全勾选 | 1 | 1 |
| [add-card-scenario-coverage](../openspec/changes/add-card-scenario-coverage/) | 7/7 | 任务全勾选 | 1 | 1 |
| [add-card-scenario-test-harness](../openspec/changes/add-card-scenario-test-harness/) | 9/9 | 任务全勾选 | 1 | 1 |
| [add-cardinstance-effect-keywords](../openspec/changes/add-cardinstance-effect-keywords/) | 3/3 | 任务全勾选 | 1 | 1 |
| [add-cardpack-code-and-environment](../openspec/changes/add-cardpack-code-and-environment/) | 7/8 | 有未勾选项 | 1 | 1 |
| [add-cardpack-editor-autocomplete](../openspec/changes/add-cardpack-editor-autocomplete/) | 5/6 | 有未勾选项 | 1 | 1 |
| [add-cardquery-pipeline-editor](../openspec/changes/add-cardquery-pipeline-editor/) | 12/12 | 任务全勾选 | 1 | 1 |
| [add-choose-fusion-act-reveal](../openspec/changes/add-choose-fusion-act-reveal/) | 23/36 | 有未勾选项 | 5 | 5 |
| [add-condition-trace-logging](../openspec/changes/add-condition-trace-logging/) | 6/6 | 任务全勾选 | 1 | 1 |
| [add-copy-mode-and-move-reset-rules](../openspec/changes/add-copy-mode-and-move-reset-rules/) | 5/5 | 任务全勾选 | 2 | 2 |
| [add-counter-variable-registration](../openspec/changes/add-counter-variable-registration/) | 0/5 | 有未勾选项 | 1 | 1 |
| [add-debug-event-console](../openspec/changes/add-debug-event-console/) | 5/6 | 有未勾选项 | 1 | 1 |
| [add-deck-management](../openspec/changes/add-deck-management/) | 19/21 | 有未勾选项 | 2 | 2 |
| [add-devtools-log-viewer](../openspec/changes/add-devtools-log-viewer/) | 5/5 | 任务全勾选 | 1 | 1 |
| [add-devtools-match-lab](../openspec/changes/add-devtools-match-lab/) | 7/8 | 有未勾选项 | 1 | 1 |
| [add-devtools-match-lab-starting-turn](../openspec/changes/add-devtools-match-lab-starting-turn/) | 5/5 | 任务全勾选 | 1 | 1 |
| [add-dsl-distinct](../openspec/changes/add-dsl-distinct/) | 0/5 | 有未勾选项 | 1 | 1 |
| [add-dsl-enum-value-menu](../openspec/changes/add-dsl-enum-value-menu/) | 3/4 | 有未勾选项 | 1 | 1 |
| [add-dsl-parentheses-encapsulation](../openspec/changes/add-dsl-parentheses-encapsulation/) | 6/8 | 有未勾选项 | 1 | 1 |
| [add-dsl-set-operators](../openspec/changes/add-dsl-set-operators/) | 5/5 | 任务全勾选 | 1 | 1 |
| [add-dual-class-deck-mode](../openspec/changes/add-dual-class-deck-mode/) | 22/22 | 任务全勾选 | 4 | 4 |
| [add-effect-chained-ability-editor](../openspec/changes/add-effect-chained-ability-editor/) | 7/7 | 任务全勾选 | 1 | 1 |
| [add-effect-failure-completion-mode](../openspec/changes/add-effect-failure-completion-mode/) | 5/5 | 任务全勾选 | 1 | 1 |
| [add-effectcompleted-generated-card-snapshots](../openspec/changes/add-effectcompleted-generated-card-snapshots/) | 5/5 | 任务全勾选 | 1 | 1 |
| [add-effectcompleted-generated-cards](../openspec/changes/add-effectcompleted-generated-cards/) | 4/4 | 任务全勾选 | 1 | 1 |
| [add-emblem-event-effect](../openspec/changes/add-emblem-event-effect/) | 6/6 | 任务全勾选 | 1 | 1 |
| [add-emblem-zone-ui](../openspec/changes/add-emblem-zone-ui/) | 6/6 | 任务全勾选 | 1 | 1 |
| [add-event-effect-ui-categories](../openspec/changes/add-event-effect-ui-categories/) | 13/13 | 任务全勾选 | 1 | 1 |
| [add-event-log-interface](../openspec/changes/add-event-log-interface/) | 5/6 | 有未勾选项 | 1 | 1 |
| [add-event-param-memberaccess](../openspec/changes/add-event-param-memberaccess/) | 5/5 | 任务全勾选 | 1 | 1 |
| [add-eventcard-dsl-function](../openspec/changes/add-eventcard-dsl-function/) | 4/4 | 任务全勾选 | 1 | 1 |
| [add-eventhistory-dsl-access](../openspec/changes/add-eventhistory-dsl-access/) | 11/12 | 有未勾选项 | 1 | 1 |
| [add-game-reconnect-control](../openspec/changes/add-game-reconnect-control/) | 6/10 | 有未勾选项 | 1 | 1 |
| [add-grant-ability-effect-ui-control](../openspec/changes/add-grant-ability-effect-ui-control/) | 9/13 | 有未勾选项 | 1 | 1 |
| [add-grant-ability-setstats-and-effect-outputs](../openspec/changes/add-grant-ability-setstats-and-effect-outputs/) | 10/10 | 任务全勾选 | 1 | 1 |
| [add-graveyard-bloodpool-necromancy-reanimate](../openspec/changes/add-graveyard-bloodpool-necromancy-reanimate/) | 8/8 | 任务全勾选 | 2 | 2 |
| [add-in-match-chat-system](../openspec/changes/add-in-match-chat-system/) | 14/18 | 有未勾选项 | 1 | 1 |
| [add-in-match-emote-system](../openspec/changes/add-in-match-emote-system/) | 47/56 | 有未勾选项 | 1 | 1 |
| [add-leave-vanish-temporary](../openspec/changes/add-leave-vanish-temporary/) | 4/4 | 任务全勾选 | 1 | 1 |
| [add-leavefield-preroute-legacy-leave-events](../openspec/changes/add-leavefield-preroute-legacy-leave-events/) | 11/12 | 有未勾选项 | 1 | 1 |
| [add-local-battle-customization](../openspec/changes/add-local-battle-customization/) | 15/18 | 有未勾选项 | 1 | 1 |
| [add-mainmenu-return-match-confirmation](../openspec/changes/add-mainmenu-return-match-confirmation/) | 6/11 | 有未勾选项 | 1 | 1 |
| [add-official-match-winrate-stats](../openspec/changes/add-official-match-winrate-stats/) | 14/14 | 任务全勾选 | 1 | 1 |
| [add-parameter-dsl-input](../openspec/changes/add-parameter-dsl-input/) | 0/7 | 有未勾选项 | 1 | 1 |
| [add-persistent-aura-refresh](../openspec/changes/add-persistent-aura-refresh/) | 14/14 | 任务全勾选 | 1 | 1 |
| [add-play-modes-and-evolve-flag](../openspec/changes/add-play-modes-and-evolve-flag/) | 8/8 | 任务全勾选 | 2 | 2 |
| [add-player-temp-variables](../openspec/changes/add-player-temp-variables/) | 5/7 | 有未勾选项 | 1 | 1 |
| [add-pre-match-deck-selection](../openspec/changes/add-pre-match-deck-selection/) | 8/9 | 有未勾选项 | 1 | 1 |
| [add-preview-description-parsing](../openspec/changes/add-preview-description-parsing/) | 5/5 | 任务全勾选 | 1 | 1 |
| [add-preview-description-tooltips](../openspec/changes/add-preview-description-tooltips/) | 7/7 | 任务全勾选 | 1 | 1 |
| [add-random-choose-effects](../openspec/changes/add-random-choose-effects/) | 6/6 | 任务全勾选 | 1 | 1 |
| [add-remove-ability-effect](../openspec/changes/add-remove-ability-effect/) | 7/7 | 任务全勾选 | 1 | 1 |
| [add-replace-deck-effect](../openspec/changes/add-replace-deck-effect/) | 7/7 | 任务全勾选 | 1 | 1 |
| [add-replay-event-journal](../openspec/changes/add-replay-event-journal/) | 29/33 | 有未勾选项 | 1 | 1 |
| [add-roomid-and-keep-gameid-per-match](../openspec/changes/add-roomid-and-keep-gameid-per-match/) | 16/21 | 有未勾选项 | 1 | 1 |
| [add-setattacktimes-effect-handler](../openspec/changes/add-setattacktimes-effect-handler/) | 4/4 | 任务全勾选 | 1 | 1 |
| [add-setcountdown-effect-handler](../openspec/changes/add-setcountdown-effect-handler/) | 3/3 | 任务全勾选 | 1 | 1 |
| [add-singleplayer-dual-control](../openspec/changes/add-singleplayer-dual-control/) | 5/6 | 有未勾选项 | 1 | 1 |
| [add-spellboost-earthsigil-system](../openspec/changes/add-spellboost-earthsigil-system/) | 10/10 | 任务全勾选 | 1 | 1 |
| [add-spellboost-effect-handler](../openspec/changes/add-spellboost-effect-handler/) | 0/3 | 有未勾选项 | 1 | 1 |
| [add-splitdamage-and-status-duration](../openspec/changes/add-splitdamage-and-status-duration/) | 10/10 | 任务全勾选 | 1 | 1 |
| [add-super-evolve-effect-handler](../openspec/changes/add-super-evolve-effect-handler/) | 5/5 | 任务全勾选 | 1 | 1 |
| [add-super-evolve-system](../openspec/changes/add-super-evolve-system/) | 7/7 | 任务全勾选 | 1 | 1 |
| [add-transaction-same-name-trigger-limit](../openspec/changes/add-transaction-same-name-trigger-limit/) | 5/5 | 任务全勾选 | 1 | 1 |
| [add-tribe-buff-effect](../openspec/changes/add-tribe-buff-effect/) | 5/5 | 任务全勾选 | 1 | 1 |
| [add-trigger-count-limits](../openspec/changes/add-trigger-count-limits/) | 4/4 | 任务全勾选 | 1 | 1 |
| [add-turn-timer](../openspec/changes/add-turn-timer/) | 6/9 | 有未勾选项 | 1 | 1 |
| [add-unified-access-control-system](../openspec/changes/add-unified-access-control-system/) | 0/22 | 有未勾选项 | 1 | 1 |
| [add-user-accounts](../openspec/changes/add-user-accounts/) | 9/9 | 任务全勾选 | 1 | 1 |
| [add-user-avatar-upload](../openspec/changes/add-user-avatar-upload/) | 6/6 | 任务全勾选 | 1 | 1 |
| [convert-carddatabases-divine-board](../openspec/changes/convert-carddatabases-divine-board/) | 2/11 | 有未勾选项 | 1 | 1 |
| [convert-carddatabases-to-card-data](../openspec/changes/convert-carddatabases-to-card-data/) | 10/11 | 有未勾选项 | 1 | 1 |
| [fix-card-editor-usability](../openspec/changes/fix-card-editor-usability/) | 12/17 | 有未勾选项 | 1 | 1 |
| [fix-lan-multiplayer-flow](../openspec/changes/fix-lan-multiplayer-flow/) | 12/15 | 有未勾选项 | 3 | 3 |
| [improve-ability-editor-dsl-ux](../openspec/changes/improve-ability-editor-dsl-ux/) | 33/36 | 有未勾选项 | 3 | 3 |
| [optimize-event-intercept-performance](../openspec/changes/optimize-event-intercept-performance/) | 6/6 | 任务全勾选 | 1 | 1 |
| [refactor-ability-runtime-id-semantics](../openspec/changes/refactor-ability-runtime-id-semantics/) | 9/15 | 有未勾选项 | 1 | 1 |
| [refactor-act-cost-buff-integration](../openspec/changes/refactor-act-cost-buff-integration/) | 5/7 | 有未勾选项 | 1 | 1 |
| [refactor-aura-ability-unified-execution](../openspec/changes/refactor-aura-ability-unified-execution/) | 9/9 | 任务全勾选 | 1 | 1 |
| [refactor-aura-buff-definition](../openspec/changes/refactor-aura-buff-definition/) | 11/11 | 任务全勾选 | 1 | 1 |
| [refactor-buff-config-structure](../openspec/changes/refactor-buff-config-structure/) | 4/7 | 有未勾选项 | 1 | 1 |
| [refactor-buff-refresh-cache](../openspec/changes/refactor-buff-refresh-cache/) | 10/10 | 任务全勾选 | 1 | 1 |
| [refactor-card-action-usage-buffs](../openspec/changes/refactor-card-action-usage-buffs/) | 14/14 | 任务全勾选 | 1 | 1 |
| [refactor-card-instance-continuity-version](../openspec/changes/refactor-card-instance-continuity-version/) | 0/0 | 缺任务文件 | 0 | 0 |
| [refactor-cardset-cardpack-separation](../openspec/changes/refactor-cardset-cardpack-separation/) | 8/8 | 任务全勾选 | 2 | 2 |
| [refactor-dsl-editor-modularization](../openspec/changes/refactor-dsl-editor-modularization/) | 25/25 | 任务全勾选 | 1 | 1 |
| [refactor-dsl-editor-single-ast-source](../openspec/changes/refactor-dsl-editor-single-ast-source/) | 0/5 | 有未勾选项 | 1 | 1 |
| [refactor-frontend-dsl-parsing-unification](../openspec/changes/refactor-frontend-dsl-parsing-unification/) | 0/5 | 有未勾选项 | 1 | 1 |
| [refactor-frontend-zone-movement-cardmoved](../openspec/changes/refactor-frontend-zone-movement-cardmoved/) | 0/9 | 有未勾选项 | 1 | 1 |
| [refactor-game-event-raw-event-pipeline](../openspec/changes/refactor-game-event-raw-event-pipeline/) | 24/25 | 有未勾选项 | 2 | 2 |
| [refactor-game-session-isolation-architecture](../openspec/changes/refactor-game-session-isolation-architecture/) | 4/19 | 有未勾选项 | 4 | 4 |
| [refactor-graveyard-move-reason](../openspec/changes/refactor-graveyard-move-reason/) | 6/6 | 任务全勾选 | 2 | 2 |
| [refactor-meta-progression](../openspec/changes/refactor-meta-progression/) | 16/17 | 有未勾选项 | 1 | 1 |
| [refactor-replay-stable-ingame-ids](../openspec/changes/refactor-replay-stable-ingame-ids/) | 4/11 | 有未勾选项 | 2 | 2 |
| [refactor-target-resolution-timing](../openspec/changes/refactor-target-resolution-timing/) | 4/5 | 有未勾选项 | 1 | 1 |
| [refactor-unified-preflight-orchestration](../openspec/changes/refactor-unified-preflight-orchestration/) | 0/15 | 有未勾选项 | 1 | 1 |
| [remove-ability-execution-kind](../openspec/changes/remove-ability-execution-kind/) | 4/5 | 有未勾选项 | 1 | 1 |
| [remove-buff-applicability-condition](../openspec/changes/remove-buff-applicability-condition/) | 2/4 | 有未勾选项 | 1 | 1 |
| [remove-card-definition-can-evolve](../openspec/changes/remove-card-definition-can-evolve/) | 4/4 | 任务全勾选 | 1 | 1 |
| [unify-turn-boundary-cleanup](../openspec/changes/unify-turn-boundary-cleanup/) | 19/19 | 任务全勾选 | 1 | 1 |
| [update-ability-tags-and-costs](../openspec/changes/update-ability-tags-and-costs/) | 6/6 | 任务全勾选 | 1 | 1 |
| [update-ability-target-selection-player-scope](../openspec/changes/update-ability-target-selection-player-scope/) | 7/7 | 任务全勾选 | 1 | 1 |
| [update-ability-trigger-ordering-by-zone-position](../openspec/changes/update-ability-trigger-ordering-by-zone-position/) | 7/7 | 任务全勾选 | 1 | 1 |
| [update-abilitytag-effectkeyword-and-cardkeywords](../openspec/changes/update-abilitytag-effectkeyword-and-cardkeywords/) | 13/36 | 有未勾选项 | 2 | 2 |
| [update-battle-log-announcements](../openspec/changes/update-battle-log-announcements/) | 5/5 | 任务全勾选 | 2 | 2 |
| [update-buff-duration-parameter-expiry](../openspec/changes/update-buff-duration-parameter-expiry/) | 0/5 | 有未勾选项 | 1 | 1 |
| [update-buff-effectkey-stacking](../openspec/changes/update-buff-effectkey-stacking/) | 4/5 | 有未勾选项 | 1 | 1 |
| [update-buff-nonstackable-key](../openspec/changes/update-buff-nonstackable-key/) | 6/6 | 任务全勾选 | 1 | 1 |
| [update-buff-removal-health-cap](../openspec/changes/update-buff-removal-health-cap/) | 3/3 | 任务全勾选 | 1 | 1 |
| [update-buff-removal-health-floor](../openspec/changes/update-buff-removal-health-floor/) | 22/22 | 任务全勾选 | 1 | 1 |
| [update-card-art-crop](../openspec/changes/update-card-art-crop/) | 6/8 | 有未勾选项 | 1 | 1 |
| [update-card-detail-view](../openspec/changes/update-card-detail-view/) | 0/7 | 有未勾选项 | 1 | 1 |
| [update-card-editor-data-cache](../openspec/changes/update-card-editor-data-cache/) | 8/11 | 有未勾选项 | 1 | 1 |
| [update-card-editor-main-layout](../openspec/changes/update-card-editor-main-layout/) | 0/6 | 有未勾选项 | 1 | 1 |
| [update-chained-ability-timing](../openspec/changes/update-chained-ability-timing/) | 0/5 | 有未勾选项 | 1 | 1 |
| [update-decision-effects-branch-params](../openspec/changes/update-decision-effects-branch-params/) | 8/10 | 有未勾选项 | 1 | 1 |
| [update-decision-event-commit-flow](../openspec/changes/update-decision-event-commit-flow/) | 9/9 | 任务全勾选 | 1 | 1 |
| [update-decision-event-resolution-timing](../openspec/changes/update-decision-event-resolution-timing/) | 5/5 | 任务全勾选 | 1 | 1 |
| [update-delay-mode-combat-resolution](../openspec/changes/update-delay-mode-combat-resolution/) | 0/8 | 有未勾选项 | 1 | 1 |
| [update-delayed-event-instance-config](../openspec/changes/update-delayed-event-instance-config/) | 5/5 | 任务全勾选 | 1 | 1 |
| [update-dsl-collection-autoboxing](../openspec/changes/update-dsl-collection-autoboxing/) | 5/5 | 任务全勾选 | 1 | 1 |
| [update-dsl-count-unary](../openspec/changes/update-dsl-count-unary/) | 14/15 | 有未勾选项 | 1 | 1 |
| [update-dsl-distinct-list-expansion](../openspec/changes/update-dsl-distinct-list-expansion/) | 12/12 | 任务全勾选 | 1 | 1 |
| [update-dsl-editor-pipeline-editing](../openspec/changes/update-dsl-editor-pipeline-editing/) | 9/9 | 任务全勾选 | 1 | 1 |
| [update-dsl-editor-ux-favorites-parse](../openspec/changes/update-dsl-editor-ux-favorites-parse/) | 55/55 | 任务全勾选 | 1 | 1 |
| [update-dsl-pipeline-inline-self-root](../openspec/changes/update-dsl-pipeline-inline-self-root/) | 7/7 | 任务全勾选 | 1 | 1 |
| [update-dsl-pipeline-stage-modal-editor](../openspec/changes/update-dsl-pipeline-stage-modal-editor/) | 9/9 | 任务全勾选 | 1 | 1 |
| [update-dsl-pipeline-strategy-ui](../openspec/changes/update-dsl-pipeline-strategy-ui/) | 6/6 | 任务全勾选 | 1 | 1 |
| [update-dsl-query-unification](../openspec/changes/update-dsl-query-unification/) | 0/7 | 有未勾选项 | 1 | 1 |
| [update-dsl-set-difference-operator](../openspec/changes/update-dsl-set-difference-operator/) | 6/8 | 有未勾选项 | 1 | 1 |
| [update-effect-duration-params](../openspec/changes/update-effect-duration-params/) | 4/4 | 任务全勾选 | 1 | 1 |
| [update-effect-target-pool-context](../openspec/changes/update-effect-target-pool-context/) | 5/5 | 任务全勾选 | 2 | 2 |
| [update-enhance-cost-selection](../openspec/changes/update-enhance-cost-selection/) | 4/4 | 任务全勾选 | 1 | 1 |
| [update-eventhistory-transaction-consistency](../openspec/changes/update-eventhistory-transaction-consistency/) | 9/9 | 任务全勾选 | 1 | 1 |
| [update-evolution-right-sharing](../openspec/changes/update-evolution-right-sharing/) | 0/3 | 有未勾选项 | 1 | 1 |
| [update-evolve-schema-and-neutral-cards](../openspec/changes/update-evolve-schema-and-neutral-cards/) | 6/6 | 任务全勾选 | 1 | 1 |
| [update-game-data-provider-double-buffer](../openspec/changes/update-game-data-provider-double-buffer/) | 5/6 | 有未勾选项 | 1 | 1 |
| [update-guest-deck-refresh-retention](../openspec/changes/update-guest-deck-refresh-retention/) | 3/5 | 有未勾选项 | 1 | 1 |
| [update-hand-card-drag-play](../openspec/changes/update-hand-card-drag-play/) | 10/11 | 有未勾选项 | 1 | 1 |
| [update-opening-hand-and-pp-boost](../openspec/changes/update-opening-hand-and-pp-boost/) | 0/9 | 有未勾选项 | 2 | 2 |
| [update-pipeline-inline-autocomplete-tooltips](../openspec/changes/update-pipeline-inline-autocomplete-tooltips/) | 4/5 | 有未勾选项 | 1 | 1 |
| [update-postprocess-projection](../openspec/changes/update-postprocess-projection/) | 0/13 | 有未勾选项 | 1 | 1 |
| [update-replay-raw-only-persistence](../openspec/changes/update-replay-raw-only-persistence/) | 10/11 | 有未勾选项 | 1 | 1 |
| [update-requestevent-preflight-decision](../openspec/changes/update-requestevent-preflight-decision/) | 11/19 | 有未勾选项 | 1 | 1 |
| [update-spellboost-event-batching](../openspec/changes/update-spellboost-event-batching/) | 4/4 | 任务全勾选 | 1 | 1 |
| [update-target-key-default](../openspec/changes/update-target-key-default/) | 2/2 | 任务全勾选 | 1 | 1 |
| [update-tribe-semantics-and-pocket-galaxy](../openspec/changes/update-tribe-semantics-and-pocket-galaxy/) | 13/20 | 有未勾选项 | 3 | 3 |
| [update-triggeringevent-dsl-binding](../openspec/changes/update-triggeringevent-dsl-binding/) | 3/3 | 任务全勾选 | 1 | 1 |

## 同一主题的多次变更

这些主题需要核对先后关系，而不是把多个增量文件直接拼接：

| Capability | 涉及变更数 |
|---|---:|
| `dsl-editor` | 12 |
| `card-data` | 4 |
| `card-editor` | 4 |
| `persistent-auras` | 4 |
| `buff-duration` | 3 |
| `card-pack-catalog` | 3 |
| `dsl-event-params` | 3 |
| `event-decision` | 3 |
| `ability-editor` | 2 |
| `ability-editor-ux` | 2 |
| `ability-effects` | 2 |
| `ability-targets` | 2 |
| `ability-triggers` | 2 |
| `buff-removal` | 2 |
| `buff-stacking` | 2 |
| `card-play-modes` | 2 |
| `card-scenario-tests` | 2 |
| `decision-effects` | 2 |
| `deck-management` | 2 |
| `devtools-match-lab` | 2 |
| `dsl-operators` | 2 |
| `dsl-query` | 2 |
| `effect-card-source` | 2 |
| `effect-completion` | 2 |
| `effects` | 2 |
| `event-debug-console` | 2 |
| `event-log-interface` | 2 |
| `evolution-system` | 2 |
| `game-reconnect` | 2 |
| `raw-event-pipeline` | 2 |
| `reanimate` | 2 |
| `spellboost-earthsigil` | 2 |
| `user-accounts` | 2 |
