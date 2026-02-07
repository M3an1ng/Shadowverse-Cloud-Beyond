# Change: Add CardInstance EffectKeywords

## Why
Ability-driven effect keywords are needed in the editor/DSL to present a card's effect tags without manual duplication.

## What Changes
- Add a CardInstance.EffectKeywords property that returns a de-duplicated collection of EffectKeyword values from the card's abilities.
- Expose EffectKeywords via DSL metadata for UI usage.
- Do not filter by silence or ability enabled state; only remove EffectKeyword.None.

## Impact
- Affected specs: card-instance
- Affected code: Server/Domain/Entities/CardInstance.cs, Server/Domain/Abilities/DSL metadata exposure