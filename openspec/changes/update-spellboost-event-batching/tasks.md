## 1. Design
- [x] 1.1 明确 SpellBoostEvent 的对外参数形式（目标列表与增幅结果的表达方式）。

## 2. Implementation (Server)
- [x] 2.1 SpellBoostEvent 接受目标卡牌列表，并在 Commit 中批量递增计数。
- [x] 2.2 PlayerState 在施放法术时仅生成一次 SpellBoostEvent（批量目标）。

## 3. Validation
- [x] 3.1 单测：施放法术时仅生成一个 SpellBoostEvent，且目标包含全部 Spellboost 卡牌。
