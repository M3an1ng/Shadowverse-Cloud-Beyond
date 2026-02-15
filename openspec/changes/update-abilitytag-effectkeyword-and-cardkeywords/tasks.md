## 0. Discovery (Baseline)
- [ ] 0.1 列出当前 `CardKeyword` 的实现覆盖面（Storm/Ward/Rush 已部分接入；Ambush/Shield/Pressure/Aura 未接入核心规则）。
- [ ] 0.2 梳理 `AbilityTag` 的数据流：DB `AbilityDefinition.AbilityTag` → `AbilityLoader` → `AbilityConfig.AbilityTag` → `Ability.AbilityTag` → 事件/DTO/日志 → 前端枚举选项。
- [ ] 0.3 确认现有 `/api/localization/enums/AbilityTag` 已映射 `EffectKeyword`，并评估新增 `EffectKeyword.System` 的兼容性。

## 1. Specification
- [ ] 1.1 定义 “targeted（指向性选择）” 的判定规则（以 `TargetConfig.PlayerChoiceOptions.IsRequired==true` 为主，必要时扩展显式配置）。
- [ ] 1.2 定义 4 个关键字的精确行为边界（对手/己方、是否影响 AoE/随机、触发移除条件等）。
- [ ] 1.3 定义 `AbilityTag` 与 `EffectKeyword` 的序列化/反序列化契约（DB 与 API 的兼容策略）。

## 2. Backend: AbilityTag → EffectKeyword
- [ ] 2.1 将 `AbilityConfig.AbilityTag` 与 `IAbility.AbilityTag` 改为 `EffectKeyword`（必要时保留过渡属性以兼容旧 JSON）。
- [ ] 2.2 为 `AbilityDefinition.AbilityTag` 增加显式转换策略（推荐 EF Core ValueConverter：enum ⇄ string），确保不需要 DB schema 迁移。
- [ ] 2.3 更新相关 DTO/事件（如 `AbilityTriggeredEventDto`）的字段类型或序列化输出，确保前端仍收到枚举名字符串。

## 3. Backend: CardKeyword 规则与目标选择管线
- [ ] 3.1 引入集中式目标过滤组件（例如 `TargetingRules`/`TargetabilityService`），供：
  - `TargetSelectionService.ResolveTargetsForAbilityAsync`
  - `TargetSelectionService.CheckTargetAvailabilityAsync`
  共同调用，保证一致性。
- [ ] 3.2 实现 `Aura`：对手 targeted 时不可被选为目标；若因此无目标则能力不可用（覆盖可用性检查与实际解析）。
- [ ] 3.3 实现 `Pressure`：无法成为攻击指定目标（攻击宣告/攻击合法性校验与可选目标列表一致）；覆盖与 `Ward` 交互导致“无合法攻击目标”的边界。
- [x] 3.4 实现 `Ambush`：对手 targeted 时不可被选为目标；（若规则要求）对手攻击宣告时不可选择为攻击目标。
- [x] 3.5 实现 `Shield`：在 `DamageEvent.Commit` 中拦截伤害并生成 `KeywordRemovedEvent(CardKeyword.Shield)`，确保只抵消一次。
- [ ] 3.6 实现 `Drain`：仅在战斗造成的伤害时触发吸血回复；能力造成伤害不得触发。
- [ ] 3.7 实现 `Bane`：仅在战斗伤害命中随从时触发必杀（无视剩余血量直接消灭）；能力造成伤害不得触发。
- [ ] 3.8 实现 `Shackle`：无法宣告攻击；在攻击合法性与 `CanAttack`/目标列表计算处保持一致（不依赖旧 `Freeze` 名称）。
- [x] 3.9 实现 `DestroyResist`：能力/效果破坏应被阻止，战斗破坏不受影响。
- [x] 3.10 实现 `VanishResist`：消失效果应被阻止（除非显式忽略抗性）。
- [x] 3.11 实现 `FreeEvolve`/`BanEvolve`：免 EP 消耗 / 禁止进化与超进化。
- [x] 3.12 实现 `Countdown`：回合开始倒数减少，归零后触发护符破坏（使用倒数减少事件）。

## 4. Tests
- [ ] 4.1 Aura 边界：场上仅 1 个 Aura 随从时，必须选择目标的能力 `CheckTargetAvailabilityAsync` 返回不可用。
- [ ] 4.2 Pressure 边界：攻击宣告时不可选择 pressure 随从；若存在 `Ward` 且唯一可攻击目标为 pressure，则攻击不可用。
- [ ] 4.3 Shield：受伤害时不扣血且移除 shield；再次受伤害正常扣血。
- [x] 4.4 Ambush：对手指向性目标选择无法选中；（若适用）攻击目标列表不包含 ambush。
- [ ] 4.5 Drain：战斗伤害触发回复；能力伤害不触发回复。
- [ ] 4.6 Bane：战斗伤害命中随从触发消灭；能力伤害不触发必杀。
- [ ] 4.7 Shackle：被施加后无法攻击宣告；移除后恢复攻击（在满足其他条件时）。
- [x] 4.8 Shield：0 点伤害也会移除护盾。
- [x] 4.9 Ambush：不可被宣告攻击；攻击后移除。
- [x] 4.10 DestroyResist：能力破坏被阻止。
- [x] 4.11 VanishResist：消失效果被阻止（未忽略抗性）。
- [x] 4.12 FreeEvolve/BanEvolve：免 EP、禁止进化/超进化。
- [x] 4.13 Countdown：回合开始倒数减少，归零后自动破坏护符。

## 5. Validation
- [ ] 5.1 `dotnet test`（Server.Tests）
- [ ] 5.2 `openspec validate update-abilitytag-effectkeyword-and-cardkeywords --strict`
