## 1. Implementation
- [x] 1.1 Add a chained abilities button in the effect accordion header and wire it to an editor panel.
- [x] 1.2 Render the chained abilities list with add/edit/delete actions bound to EffectConfig.ChainedAbilities.
- [x] 1.3 Implement chained ability creation using AbilityEditor with the trigger locked to EffectCompletedEvent.
- [x] 1.4 Implement drag-and-drop ordering and persist the sequence.
- [x] 1.5 Ensure save/load compatibility for chained abilities in the effect editor.
- [x] 1.6 Update styles for the chained abilities editor panel and drag states.
- [x] 1.7 Add frontend tests if applicable, otherwise document manual checks.

## 2. Manual Checks
- Open the card editor and edit an ability effect; confirm the chained abilities button appears in the effect header.
- Add a chained ability, verify the trigger step is locked to EffectCompletedEvent, and save.
- Edit and delete chained abilities from the list; reorder via drag and confirm the saved order persists.
