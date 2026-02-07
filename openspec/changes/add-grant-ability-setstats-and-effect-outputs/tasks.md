## 1. Design
- [x] 1.1 Define GrantAbility and SetSetStatus semantics plus EffectCompletedEvent.GeneratedCards sourcing rules.

## 2. Implementation
- [x] 2.1 Add EffectType entries and parameter classes for GrantAbility and SetSetStatus; register metadata/category.
- [x] 2.2 Implement GrantAbilityEffectHandler using BuffService + AbilityBuffEffect (duration and non-stackable supported).
- [x] 2.3 Extend StatChangedEvent to accept a modifier layer (default Additive) and apply it in Commit.
- [x] 2.4 Implement SetStatsEffectHandler to emit StatChangedEvent with Set layer for attack/health/cost.
- [x] 2.5 Extend EffectCompletedEvent with GeneratedCards and populate it in EffectExecutor.
- [x] 2.6 Update Forestcraft crest same-name summon to query graveyard crests by name.

## 3. Tests
- [x] 3.1 GrantAbility adds/removes abilities according to duration.
- [x] 3.2 SetSetStatus sets stats using set-layer modifiers.
- [x] 3.3 Chained ability can target EffectCompletedEvent.GeneratedCards.

