## Context
Ability target selection is currently used for both players and cards, which makes it harder to author effects and creates confusion when an effect expects a player target but the UI presents card selection.

## Goals / Non-Goals
- Goals:
  - Make "Target" mean card instance selection only.
  - Provide an explicit PlayerScope parameter for player-oriented effects.
  - Preserve existing behavior by defaulting PlayerScope to Self when missing.
- Non-Goals:
  - Introduce a TargetKind layer at the ability level.
  - Require explicit target selection for effects that do not use targets.

## Decisions
- Decision: Use PlayerScope (Self/Opponent/Both) as an effect parameter for player-oriented effects.
  - Rationale: Keeps Ability targets focused on card instances and moves player selection to effect intent.
- Decision: Do not add TargetKind; effects declare whether they use card targets in descriptions and validations.

## Alternatives considered
- Add TargetKind to Ability (Card/Player/None): rejected to avoid new ability-level branching and to keep Target usage consistent.
- Overload CardDefinitionId to accept instance IDs: rejected due to ambiguous semantics and UI confusion.

## Risks / Trade-offs
- Some existing configs rely on explicit player targets. Mitigation: default PlayerScope to Self and provide migration warnings.
- Effects that need both player scope and card targets must be clear in UI descriptions.

## Migration Plan
- Introduce PlayerScope with default Self in serialization/deserialization.
- Update metadata so UI exposes PlayerScope for player-oriented effects.
- Update handlers to resolve player targets from PlayerScope when effect targets are empty or non-player.

## Open Questions
- Which effects should support Opponent/Both vs only Self?
- Do any system effects require explicit player selection beyond scope?