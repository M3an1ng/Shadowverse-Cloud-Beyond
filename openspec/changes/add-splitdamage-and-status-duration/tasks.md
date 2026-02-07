## 1. Design
- [x] 1.1 Define split damage distribution rules in effect spec and map to implementation.
- [x] 1.2 Define status duration enum for ModifyStats (SetStatus) and duration mapping.
- [x] 1.3 Define reset-attack effect + event contract (fields, trigger behavior).

## 2. Implementation
- [x] 2.1 Extend DamageEffectParams with a distribution mode enum and update DamageEffectHandler.
- [x] 2.2 Add ResetAttackTimes effect params/handler and AttackTimesReset event.
- [x] 2.3 Add duration enum to ModifyStatsEffectParams and propagate to StatChangedEvent/BuffService.
- [x] 2.4 Update effect metadata/category mappings and localization if needed.

## 3. Tests
- [x] 3.1 Add split damage distribution tests.
- [x] 3.2 Add temporary status duration tests (end of turn removal).
- [x] 3.3 Add reset attack times tests.
