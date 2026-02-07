# Design: Keyword Targeting Rules + AbilityTag Typing

## Goal
在不重写现有 Query DSL 的前提下，将“关键字影响目标选择”的规则落在 **TargetSelectionService 的统一后置过滤层**，并让 AbilityTag 在代码层与前端枚举源对齐为 `EffectKeyword`。

## Key Decisions
1. **Targeted 的判定**
   - 默认以 `TargetConfig.PlayerChoiceOptions?.IsRequired == true` 作为“指向性目标选择（targeted）”的判定条件。
   - 若未来出现“自动选择但仍算指向性”的效果（或反之），再引入显式 `TargetingIntent` 字段扩展配置。

2. **规则落点**
   - 不修改 `QueryExecutor` 的 DSL 语义（保持其仅负责产出候选池）。
   - 在 `TargetSelectionService` 内对 `potentialTargets` 做一致的后置过滤：
     - `CheckTargetAvailabilityAsync` 与 `ResolveTargetsForAbilityAsync` 共用同一函数，保证“可用性”和“实际选择”一致。

3. **关键词规则（最小可用定义）**
   - `Aura` / `Ambush`: 无法成为能力指定目标（ability targeted）。默认限定为“对手的 targeted 目标选择”；不影响 AoE/随机，除非这些效果同样走 targeted 的 TargetConfig。
   - `Pressure`: 无法成为攻击指定目标（attack declared target），即攻击宣告与攻击合法性校验必须将其排除在可攻击目标之外。
   - `Shield`: 伤害结算时抵消一次并移除关键字（事件驱动：`DamageEvent.Commit` → `KeywordRemovedEvent`）。
   - `Drain`: 仅对“战斗伤害（来自攻击动作）”生效；能力/效果造成的伤害不触发吸血。
   - `Bane`: 仅对“战斗伤害（来自攻击动作）”生效；能力/效果造成的伤害不触发必杀。
   - `Shackle`: 仅影响攻击宣告（cannot attack）——具有该关键字的随从无法宣告攻击；不改写其被动状态计算以外的其他规则。

4. **AbilityTag 类型统一与兼容**
   - 代码层 `AbilityTag` 使用 `EffectKeyword`；
   - 存储层仍保留 string（枚举名）以避免 DB schema 迁移，并通过显式转换保障读写一致；
   - 对外（HTTP/SignalR）仍输出枚举名字符串，前端无需改动。

## Open Questions (for review)
- `Ambush` 是否同时禁止“被攻击宣告为目标”（类似潜行/无法被攻击）？若是，需要在战斗宣告逻辑（`CardInstance.CanPerformAttack` 等）加规则过滤。
- `Aura` 与 `Ambush` 是否对“己方能力”也生效？本提案默认仅对手 targeted 生效（否则会影响大量自定义能力的可玩性）。
- `Drain/Bane` 的“战斗伤害”边界：是否包含反击伤害（当前实现中同样由攻击动作触发的伤害事件）？本提案倾向于包含反击（仍属于战斗结算的一部分），但需在实现阶段确认。
