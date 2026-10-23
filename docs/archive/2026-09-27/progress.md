# 项目进展 (Project Progress)

## 里程碑 1: 概念验证与网页版核心 Demo

### 任务: M13 - 通用卡牌筛选器实现 (`CardQueryExecutor`)

*   **状态:** 进行中
*   **子任务:**
    *   [x] 定义 `CardQueryConfig`, `FilterConfig`, `SourcePoolConfig`, `OrderingConfig`, `PostProcessingConfig`, `LimitConfig` 及相关枚举 (`QueryEnums.cs`)。
    *   [x] 实现 `ICardQueryExecutor` 接口和 `CardQueryExecutor` 类骨架。
    *   [x] 实现 `GetInitialPoolAsync` 方法，支持从不同来源获取初始卡牌池。
    *   [x] 实现 `ApplySingleFilter` 方法，支持大部分基础筛选器 (`CardType`, `Cost`, `Attack`, `Health`, `CardName`, `CardId`, `Trait`, `IsEvolved`, `Location`, `IsDamaged`, `HasKeyword`, `Controller`, `Rarity`, `CardSet`, `CanAttack`, `CanEvolve`, `AttackCountThisTurn`)。
    *   [x] 在 `ApplySingleFilter` 中集成 `ValueSource` 解析逻辑 (`ResolveValueSource`, `ResolveDynamicPath`)。
    *   [x] 实现 `ApplyOrdering` 方法，支持基于 `FilterType` 的多条件排序。
    *   [x] 修复 `ApplyOrdering` 中的编译错误 (CS1503 - 类型转换) 和警告 (CS8620 - 可空性)。
    *   [x] 在 `ApplySingleFilter` 中为 `HasEffect`, `IsTargetable` 添加日志占位符。
    *   [x] 在 `ApplySingleFilter` 中为 `And`, `Or`, `Not` 添加日志占位符，标记需要重构。
    *   [x] 实现 `ApplyPostProcessing` 的基本骨架（返回原列表，添加日志）。
    *   [x] 实现 `ApplyLimit` 的基本骨架（处理静态 `MaxCount`，添加日志，标记 `MaxCountSource` 未处理）。
    *   [x] 验证 `CardInstanceDto` 属性假设。
    *   [ ] (下一步) 重构 `CardQueryExecutor` 以支持新的流水线查询模型 (DSL Source + Pipeline)。
    *   [ ] (后续) 修复 `Server.Tests` 编译错误并添加测试。
    *   [ ] (后续) 实现复杂筛选器 (`And/Or/Not`, `HasEffect`, `IsTargetable`)。


### 任务: M10/M11/M12 - 能力系统基础

*   **状态:** 部分完成
*   **子任务:**
    *   [x] 设计文档 M10.1 (触发器), M11 (目标选择), M12 (效果) 初稿完成。
    *   [x] 定义 `AbilityConfig`, `TriggerConfig`, `ConditionConfig`, `TargetConfig`, `EffectConfig` 等核心配置类。
    *   [x] 定义 `ValueConfig` 用于动态值解析。
    *   [x] 采用事件驱动架构 (EDA) 思想。
    *   [ ] 具体能力效果实现 (待 M13 完成后)。
    *   [ ] 事件处理流程实现 (待 M13 完成后)。

### 任务: 基础架构搭建

*   **状态:** 进行中
*   **子任务:**
    *   [x] 初始化 .NET 8 ASP.NET Core 项目 (`Server`)。
    *   [x] 配置 EF Core 和 SQLite 数据库。
    *   [x] 实现基础 `CardDefinition` 实体和 `ICardRepository`。
    *   [x] 添加基础 DTOs (`CardInstanceDto`, `PlayerStateDto`, `GameStateDto` 等)。
    *   [x] 配置 Serilog 日志。
    *   [x] 配置 Swagger API 文档。
    *   [ ] 初始化 Git 仓库。
    *   [ ] 修复 `Server.Tests` 项目。

## 架构决策记录 (Architectural Decisions)

### 2025-06-06: 能力配置 (`AbilityConfig`) 存储方案

*   **讨论:** 对比了两种存储 `AbilityConfig` 的方案：
    1.  **JSON + 嵌入式 DSL:** 将 `AbilityConfig` C# 对象序列化为 JSON 存储，DSL 字符串仅用于特定字段（如 `ValueConfig.Formula`）。
    2.  **纯 DSL 字符串:** 将整个能力逻辑表示为一个自定义 DSL 字符串，加载时需完整解析。
*   **决策:** **采用方案 1 (JSON + 嵌入式 DSL)**。
*   **理由:**
    *   **加载性能:** JSON 反序列化远快于自定义 DSL 解析。
    *   **开发/维护成本:** 利用标准 JSON 库，避免维护复杂解析器。
    *   **关注点分离:** 配置结构 (JSON) 与动态逻辑 (DSL Evaluator) 分离。
    *   **工具支持:** 标准 JSON 和 C# 工具支持更好。
*   **影响:** 数据库中存储能力的字段将使用 JSON 格式。加载配置后，`DslEvaluator` 仍需按需解释执行 JSON 中嵌入的 DSL 字符串。

## 里程碑 2: 网页版功能扩展 (规划中)
## 里程碑 3: 技术选型与 Unity 迁移准备 (规划中)
## 里程碑 4: Unity 版本深化与网站功能转型 (规划中)

*(注意: 此文档用于跟踪主要任务和子任务的完成情况)*

## 2026-04-05: update-hand-card-drag-play

- Original prompt: 根据该方案开始执行任务
- Added hand-card drag-to-play as the primary battle gesture in `src/ui.js`, reusing existing `playCard` and `PlayerChoiceRequested` flows.
- Removed double-click hand-card play path from the battle click handler to avoid conflict with drag release and preview click behavior.
- Added transient drag ghost, cast-lane guidance, direct-target hover feedback, invalid-drop snap-back, and reduced-motion styling in `src/styles/game.css`.
- Added regression coverage in `tests/ui_hand_drag_play.test.js` for preview click, valid drag, direct target drag, cast-area fallback, invalid drop, reduced motion, and button fallback.
- Verification so far: `npx jest tests/ui_hand_drag_play.test.js --runInBand`, `npx jest tests/ui_choice_target_rendering.test.js tests/ui_fusion_button.test.js --runInBand`, `npm run build`.
- Browser smoke so far: entered a real single-player battle, captured live mid-drag screenshot with visible hand ghost + cast lane (`output/sp-drag-live-mid.png`), and verified a live invalid drop returns to hand without spawning a field card.
- Remaining TODO: run one more live smoke path with an actual target-requiring card/state so the PlayerChoice handoff is verified without test-time state mutation.

## 2026-04-06: first-load drag latency mitigation

- Root-cause mitigation implemented in `src/ui.js`, `src/main.js`, and `index.html`.
- Menu route overlay backgrounds no longer eager-load from initial HTML; they now use `data-src` and are loaded on first overlay activation to reduce first-page image contention.
- Card art preloading now runs through a throttled queue with priority levels instead of firing unbounded `Image()` probes for every visible card at once.
- Drag interactions now defer new low-priority art preload work while the pointer is active, so ghost movement keeps frame budget priority.
- Preview/detail art requests are marked high priority; emblem art is low priority; regular card face art is normal priority.
- Verification: `npx jest tests/ui_hand_drag_play.test.js --runInBand`, `npm run build`.
- Remaining TODO: run a real browser smoke focused on “cold page load -> first battle entry -> immediate hand drag” and compare with a warm reload to confirm the first-load hitch is materially reduced.

## 2026-04-06: cold-start diagnostics hardening

- Added `src/debugDiagnostics.js` to flatten perf/debug payloads into single-line JSON strings and to assign stable per-connection debug IDs.
- Updated `src/ui.js` hand-drag PERF logs to emit export-friendly JSON, so browser `.log` files now preserve `rafDelayMs`, `menuStarrySkyAnimating`, and related fields instead of collapsing to `Object`.
- Updated `src/main.js` SignalR diagnostics to log connection creation/start lifecycle, handler registration completion, and first receipts for critical events with `connectionId`.
- Added `tests/debugDiagnostics.test.js` to cover debug log formatting and connection ID stability.
- Verification: `npx jest tests/debugDiagnostics.test.js --runInBand`, `npx jest tests/ui_hand_drag_play.test.js --runInBand`, `npm run build`.
- Remaining TODO: reproduce once more and inspect whether `No client method with the name ... found` lines correlate with missing `register handlers complete` or mismatched `connectionId` sequences.

## 2026-07-06: IgnoreWard attack targeting client validation fix

- Root cause: `src/gameManager.js` repeated Ward target validation in `selectAttackTarget` but did not exempt attackers with `IgnoreWard`, so the browser rejected non-Ward targets before the server-side `CanPerformAttack` logic could run.
- Updated `selectAttackTarget` so `IgnoreWard` attackers bypass the Ward-only target restriction while still enforcing leader attack rules such as summoning sickness and Storm.
- Added `tests/gameManager_attack_ignore_ward.test.js` to reproduce the blocked non-Ward attack and prevent regression.
- Verification: `npx jest tests/gameManager_attack_ignore_ward.test.js --runInBand`, `npx jest tests/gameManager_attack_ignore_ward.test.js tests/ui_attack_drag_leader_zone.test.js tests/gameManager_zoneStayTurn.test.js --runInBand`, `npm run build`.
