# Enemy Hand Leader Attack Zone Design

## Summary

Add a new drag-target experience for follower attacks: while the local player is dragging an attack arrow, the opponent hand area can act as a visual proxy for attacking the opposing leader.

The interaction is intentionally aligned with the existing hand-card drag battlefield highlight language:

- The enemy-hand leader attack zone appears only during attack dragging.
- The zone is visually represented by a rectangular highlight over the opponent hand area.
- The center marker uses the opponent player's avatar as the leader emblem.
- When the attack arrow enters the zone, the zone highlights as a valid leader target area.
- The arrow remains free and does not snap to the avatar.
- Releasing inside the zone is treated as attacking the opposing leader.
- On successful release, a light confirmation effect plays: the avatar flashes briefly and the zone pulses/collapses before the normal combat resolution continues.

This is a frontend interaction and presentation change. The gameplay meaning remains unchanged: the real target is still the opposing leader.

## Goals

- Make attacking the opposing leader easier to discover during drag-based attack interaction.
- Reuse the existing visual language established by hand-card drag drop-lane feedback.
- Keep the interaction visually lightweight outside of attack dragging.
- Avoid introducing a second interpretation where users think they are attacking a hand card instead of the leader.

## Non-Goals

- No always-visible leader attack affordance in the opponent hand area.
- No arrow snapping or magnetic target behavior.
- No backend rules changes for combat legality.
- No replacement of the existing direct avatar target path unless current logic already requires it.

## User Experience

### Visibility Rules

- The enemy-hand leader attack zone is hidden by default.
- It appears only after the player starts dragging a valid attacker.
- It is anchored to the opponent hand container, not to individual hand cards.
- It disappears immediately when drag ends, is cancelled, or resolves onto another target.

### Visual Treatment

- Use a square-corner rectangular outline over the enemy hand region.
- Use the opponent avatar as the central emblem inside the zone.
- Show a short label equivalent to "attack leader" near the emblem when the zone is visible.
- Keep the base visible state subtle and the hovered valid state clearly brighter.
- If the leader is not a valid target, the zone may still be visible during drag, but must use invalid styling consistent with the rest of the attack target feedback.

### Hover and Release Behavior

- When the drag arrow enters the enemy-hand leader zone, the zone switches to highlighted state.
- The arrow endpoint remains under the pointer; there is no auto-alignment to the avatar.
- When the player releases inside a valid zone, the UI resolves this as a leader attack.
- After release, play a light confirmation effect:
  - opponent avatar flashes once
  - zone briefly pulses or tightens
  - then normal combat resolution continues

## Interaction Model

### Target Semantics

The enemy-hand leader zone is a visual proxy for the opposing leader UID. It is not a new gameplay object.

Expected mapping:

- pointer over enemy field follower -> target that follower UID
- pointer over enemy avatar -> target opposing leader UID
- pointer over enemy-hand leader zone -> target opposing leader UID

### Validity Rules

The zone must respect existing combat legality. It should only resolve as valid if the opposing leader is already a valid target under the current attack state.

Examples:

- If the opposing leader is directly attackable, the zone is valid.
- If ward or other restrictions prevent leader attacks, the zone must not present as valid.
- If the drag is cancelled or leaves all valid targets, the zone must clear its highlight state.

This prevents the zone from visually promising an action the rules engine will reject.

## Technical Design

### Frontend Components

Add a dedicated transient overlay element for the enemy-hand leader attack zone, parallel to the current transient attack-drag visuals.

Responsibilities:

- create and manage a single overlay element during attack drag
- position it from the opponent hand container rect
- render opponent avatar marker within the zone
- switch between hidden, neutral, valid, and invalid states
- clean up fully on drag end or render reset

### Target Resolution

Extend the existing attack drag target resolution so that it checks the enemy-hand leader zone after direct card/avatar hit-testing and before falling back to invalid state.

Resolution rule:

- if pointer is inside the computed enemy-hand leader zone rect
- and the opposing leader UID is in the valid attack target set
- return the opposing leader as the resolved target

If the pointer is inside the zone but the leader is not a valid target, return an invalid target state for styling only, without changing gameplay semantics.

### State Flow

1. Player starts dragging a valid attacker.
2. UI creates or updates the enemy-hand leader zone overlay.
3. Pointer moves:
   - direct target hit test still works as before
   - enemy-hand leader zone rect is evaluated
   - hover styling updates accordingly
4. Player releases:
   - if dropped on the enemy-hand leader zone and valid, submit leader target UID
   - play light confirmation effect
5. Cleanup:
   - remove zone overlay
   - clear hover classes
   - clear temporary confirmation state after animation

### Data Dependencies

The design should rely on already-available frontend state:

- opponent hand container DOM rect
- opponent avatar art or avatar identity already rendered in the HUD
- opposing leader UID resolution logic already used by avatar targeting
- current attack valid target list

No new server DTOs should be required unless existing client state cannot provide the opponent avatar marker.

## Error Handling and Safety

- If the opponent hand container is missing, do not create the zone; preserve existing attack behavior.
- If the opposing leader UID cannot be resolved, the zone must not resolve as a valid target.
- If avatar media is unavailable, fall back to a safe emblem shell rather than blocking the interaction.
- Cleanup must be defensive so transient overlays cannot remain stuck after cancellation, reconnect, or rerender.

## Testing Strategy

### Automated Tests

Add focused tests for:

- attack dragging shows the enemy-hand leader zone only while dragging
- pointer entering the opponent hand zone resolves to opposing leader UID when valid
- pointer entering the zone does not snap the arrow endpoint
- release inside a valid zone submits the opposing leader UID
- zone remains invalid when opposing leader is not attackable
- cleanup removes the zone on cancel, invalid drop, or normal completion

### Manual Verification

Verify the following in-browser:

- dragging a follower toward the top hand region reveals the zone
- valid hover state is visually distinct from neutral and invalid states
- leader attack through the zone feels natural with both hidden and face-up opponent hands
- ward or similar restrictions correctly suppress valid leader-zone feedback
- the game log and other HUD layers are not unintentionally obscured by the new zone

## Tradeoffs

### Why this design

- It improves discoverability without adding persistent clutter.
- It preserves player control by not snapping the pointer.
- It stays consistent with the project's newer drag/highlight interaction language.

### Why not arrow snapping

- Snapping would be more explicit, but it makes the interaction feel heavier and less natural.
- The chosen free-arrow behavior better preserves current drag feel.

### Why use the enemy hand area

- It gives the leader attack a larger, easier-to-hit target region at the top of the battlefield.
- The avatar emblem prevents the region from reading as "attack a hand card."

## Open Questions

None for implementation entry.

The approved design decisions are:

- show only during attack dragging
- highlight on entering the enemy upper hand area
- use opponent avatar as the leader emblem
- keep arrow free, no snapping
- use light confirmation feedback on release
