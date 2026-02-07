# Change: Add Grant Ability Effect UI Control

## Why
Grant Ability effect parameters are currently edited via a raw TextArea containing JSON. This is error-prone, unfriendly, and blocks non-technical users from safely creating or reviewing granted abilities.

## What Changes
- Add a dedicated UI control type for editing Grant Ability's ability list.
- Update GrantAbilityEffectParams metadata to use the new control.
- Implement a structured list editor that can add/edit/remove abilities and show per-ability summaries.
- Persist ability configs as a structured list in effect parameters (not a JSON string).

## Impact
- Affected specs: ability-editor
- Affected code: Server/Domain/Abilities/Effects/Handlers/Parameters/Metadata/UiControlType.cs, Server/Domain/Abilities/Effects/Handlers/Parameters/GrantAbilityEffectParams.cs, src/components/EffectConfigEditor.js, src/styles/game.css
