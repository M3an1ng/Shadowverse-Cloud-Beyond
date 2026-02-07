## ADDED Requirements

### Requirement: Aura MUST Block Opponent Targeted Selection
当随从具有 `CardKeyword.Aura` 时，它在对手进行“能力指定目标（ability targeted）”时 MUST NOT 成为可选目标。

#### Scenario: Only Aura follower makes targeted ability unavailable
- **GIVEN** the opponent battlefield has exactly 1 follower
- **AND** that follower has `CardKeyword.Aura`
- **AND** an ability requires the player to choose an enemy follower target (ability targeted)
- **WHEN** the player checks playability / target availability
- **THEN** the ability MUST be considered unavailable due to having no valid targets

### Requirement: Pressure MUST Block Attack Target Declaration
当随从具有 `CardKeyword.Pressure` 时，它 MUST NOT 成为攻击指定目标（attack declared target）。

#### Scenario: Pressure follower cannot be attacked
- **GIVEN** the defending battlefield has a follower with `CardKeyword.Pressure`
- **WHEN** the attacker declares an attack against that follower
- **THEN** the attack MUST be rejected as invalid

#### Scenario: Ward + Pressure can yield no valid attack targets
- **GIVEN** the defending battlefield has at least 1 `Ward` follower
- **AND** every `Ward` follower has `CardKeyword.Pressure`
- **WHEN** the attacker tries to declare an attack this turn
- **THEN** the attacker MUST have no valid attack targets (cannot attack)

### Requirement: Ambush MUST Block Opponent Targeted Selection
当随从具有 `CardKeyword.Ambush` 时，它在对手进行 targeted 目标选择时 MUST NOT 成为可选目标。

#### Scenario: Ambush cannot be selected by opponent targeted ability
- **GIVEN** a follower with `CardKeyword.Ambush` is on the battlefield
- **AND** the opponent uses a targeted ability that can normally target enemy followers
- **WHEN** the server provides the valid target list
- **THEN** the ambush follower MUST NOT appear in the valid targets

#### Scenario: Ambush follower cannot be declared as attack target
- **GIVEN** a follower with `CardKeyword.Ambush` is on the battlefield
- **WHEN** the opponent declares an attack against that follower
- **THEN** the attack MUST be rejected as invalid

#### Scenario: Ambush is removed after the ambush follower attacks
- **GIVEN** a follower with `CardKeyword.Ambush` declares an attack
- **WHEN** the attack is committed
- **THEN** the attacker MUST lose `CardKeyword.Ambush`

### Requirement: Shield MUST Negate Next Damage Once
当随从具有 `CardKeyword.Shield` 时，下一次将要对其造成的伤害 MUST 被抵消（不降低生命值），并且 `Shield` MUST 在结算后被移除。

#### Scenario: Shield prevents one damage instance then is removed
- **GIVEN** a follower has `CardKeyword.Shield`
- **WHEN** it would take damage from a `DamageEvent`
- **THEN** its health MUST NOT decrease due to that damage
- **AND** the server MUST remove `CardKeyword.Shield`
- **AND** subsequent damage MUST apply normally

#### Scenario: Shield is consumed by zero damage
- **GIVEN** a follower has `CardKeyword.Shield`
- **WHEN** it would take zero damage from a `DamageEvent`
- **THEN** the server MUST remove `CardKeyword.Shield`

### Requirement: Drain MUST Only Trigger On Combat Damage
当随从具有 `CardKeyword.Drain` 时，它造成战斗伤害（combat damage, from attacks）时，MUST 为其控制者回复等同于实际造成伤害的生命值；由能力/效果造成的伤害 MUST NOT 触发吸血。

#### Scenario: Drain heals on attack damage
- **GIVEN** an attacking follower has `CardKeyword.Drain`
- **AND** it deals combat damage to an enemy follower or leader
- **WHEN** the combat damage is committed
- **THEN** the attacker controller MUST be healed by the actual damage amount dealt

#### Scenario: Drain does not heal on ability damage
- **GIVEN** a follower has `CardKeyword.Drain`
- **AND** damage is dealt by an ability/effect (non-combat)
- **WHEN** that damage is committed
- **THEN** the attacker controller MUST NOT be healed due to `Drain`

### Requirement: Bane MUST Only Trigger On Combat Damage To Followers
当随从具有 `CardKeyword.Bane` 时，它对敌方随从造成战斗伤害时，MUST 令被伤害的随从被消灭（无视剩余生命值）；由能力/效果造成的伤害 MUST NOT 触发必杀。

#### Scenario: Bane destroys follower on attack damage
- **GIVEN** an attacking follower has `CardKeyword.Bane`
- **AND** it deals combat damage to an enemy follower
- **WHEN** the combat damage is committed
- **THEN** the damaged follower MUST be destroyed regardless of remaining health

#### Scenario: Bane does not apply on ability damage
- **GIVEN** a follower has `CardKeyword.Bane`
- **AND** damage is dealt by an ability/effect (non-combat)
- **WHEN** that damage is committed
- **THEN** the damaged follower MUST NOT be destroyed due to `Bane`

### Requirement: Shackle MUST Prevent Attack Declaration
当随从具有 `CardKeyword.Shackle` 时，它 MUST NOT 能宣告攻击（declare attack），即使其满足其他可攻击条件（例如已过召唤回合、或具有 Rush/Storm、或已进化）。

#### Scenario: Shackled follower cannot declare attack
- **GIVEN** an attacking follower has `CardKeyword.Shackle`
- **AND** it otherwise would be able to attack (attack count not exceeded, has attack > 0, etc.)
- **WHEN** the player declares an attack with that follower
- **THEN** the attack MUST be rejected as invalid

#### Scenario: Shackle removal restores attack eligibility
- **GIVEN** an attacking follower previously had `CardKeyword.Shackle`
- **AND** the `Shackle` keyword is removed
- **WHEN** the player declares an attack with that follower
- **THEN** the attack MUST be allowed if all other attack conditions are met

### Requirement: Target Availability MUST Match Target Resolution
目标可用性检查（playability）与目标解析（实际执行）在关键字规则上 MUST 保持一致，避免“可用但无法选择目标”的分歧（本变更范围内重点覆盖 `Aura/Ambush`）。

#### Scenario: Availability and resolution use same keyword filtering
- **GIVEN** an ability has a target with `RequireValidTarget == true`
- **WHEN** `CheckTargetAvailabilityAsync` reports the ability is available
- **THEN** `ResolveTargetsForAbilityAsync` MUST NOT fail due to keyword-based targetability rules

### Requirement: DestroyResist MUST Block Ability Destruction
当随从具有 `CardKeyword.DestroyResist` 时，由能力/效果导致的摧毁 MUST 被阻止。

#### Scenario: DestroyResist prevents destroy effect
- **GIVEN** a follower has `CardKeyword.DestroyResist`
- **WHEN** a destroy effect attempts to destroy that follower
- **THEN** the follower MUST remain on the battlefield

### Requirement: VanishResist MUST Block Vanish Effects Unless Ignored
当随从具有 `CardKeyword.VanishResist` 时，消失效果 MUST 被阻止，除非明确配置忽略抗性。

#### Scenario: VanishResist blocks vanish when not ignored
- **GIVEN** a follower has `CardKeyword.VanishResist`
- **WHEN** a vanish effect is applied with ignore-resist disabled
- **THEN** the follower MUST remain in its current zone

### Requirement: FreeEvolve MUST Skip EP Consumption
当随从具有 `CardKeyword.FreeEvolve` 时，进化 MUST 不消耗 EP（不影响超进化消耗），但仍需满足进化解禁与本回合进化权限。

#### Scenario: FreeEvolve does not consume evolution points
- **GIVEN** a follower has `CardKeyword.FreeEvolve`
- **AND** the player has evolution unlocked and evolution right this turn
- **AND** the player has 0 EP
- **WHEN** the player evolves that follower
- **THEN** the evolution MUST succeed
- **AND** the player’s EP MUST remain unchanged

### Requirement: BanEvolve MUST Block Evolution
当随从具有 `CardKeyword.BanEvolve` 时，进化与超进化 MUST 被禁止。

#### Scenario: BanEvolve blocks evolve and super evolve
- **GIVEN** a follower has `CardKeyword.BanEvolve`
- **WHEN** the player attempts to evolve or super evolve that follower
- **THEN** the evolution request MUST be rejected

### Requirement: Countdown MUST Decrease At Owner Turn Start
当护符具有 `CardKeyword.Countdown` 时，它在自己的回合开始时 MUST 将倒数减少 1；倒数归零时 MUST 触发护符破坏。

#### Scenario: Countdown decreases at turn start and destroys at zero
- **GIVEN** an amulet has `CardKeyword.Countdown` with countdown value 1
- **WHEN** its owner’s turn starts
- **THEN** the countdown MUST become 0
- **AND** the amulet MUST be destroyed
