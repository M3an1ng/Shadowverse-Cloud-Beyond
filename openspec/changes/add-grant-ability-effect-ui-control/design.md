## Context
The Grant Ability effect currently exposes a TextArea for a JSON array, which is a poor UX and leads to invalid configs. A dedicated editor should reuse existing ability authoring UI while keeping effect parameters structured.

## Goals / Non-Goals
- Goals: Provide a list-based editor for Grant Ability, enable add/edit/remove/reorder, allow copy from the ability library, show summaries, and persist structured configs.
- Non-Goals: Redesign the entire ability editor UI or introduce new back-end models.

## Decisions
- Decision: Introduce a new UiControlType (e.g., AbilityListEditor) and map it to a custom UI control in EffectConfigEditor.
- Decision: Reuse AbilityEditor in a modal or inline panel for authoring list entries; convert its output into AbilityConfig objects.
- Decision: Store ability configs as arrays (not JSON strings) so ValueConfig can carry structured data.
- Decision: Reuse the card editor "Copy Existing" ability selection UX to populate the list.
- Decision: Provide list ordering controls (move up/down or drag-and-drop) and persist order.

## Risks / Trade-offs
- Nested editors can increase DOM complexity and require careful scroll/overlay handling.
- Reusing AbilityEditor may need light refactoring to share summary formatting logic.

## Migration Plan
- Update metadata to the new control type and keep backward compatibility by accepting existing string payloads during rendering (parse when detected).

## Open Questions
- What is the minimum summary content expected by designers (trigger name vs full trigger details)?
