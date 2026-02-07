## 1. Implementation
- [x] 1.1 Add UiControlType.AbilityListEditor (or equivalent) and expose it in effect parameter metadata.
- [x] 1.2 Switch GrantAbilityEffectParams.AbilityConfigs to the new control type and ensure defaults are list-friendly.
- [x] 1.3 Implement a Grant Ability list editor UI (list + add/edit/delete) that uses AbilityEditor for authoring.
- [x] 1.4 Add list reordering controls (move up/down or drag) and persist order.
- [x] 1.5 Add "Copy Existing" entry flow to pull abilities from the library.
- [x] 1.6 Wire EffectConfigEditor.renderParameterControl to render the new control and store array payloads (not JSON strings).
- [x] 1.7 Render concise summaries per entry (name + trigger name).
- [x] 1.8 Add styles for the new control in src/styles/game.css (list rows, empty state, actions, modal host).
- [x] 1.9 Add/adjust unit tests or manual verification steps for list editing, reorder, and copy flows.

## 2. QA
- [ ] 2.1 Manual: open ability editor -> Grant Ability effect -> add ability -> save; reopen and confirm list renders with the saved summary.
- [ ] 2.2 Manual: edit an existing entry; confirm the summary updates and the stored parameters remain valid.
- [ ] 2.3 Manual: reorder entries; confirm save preserves order.
- [ ] 2.4 Manual: copy existing ability; confirm it is inserted as a new entry with correct summary.
