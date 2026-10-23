# Review Checklist

Review findings in this order: correctness, readability, impact, performance.

## Functional Checks

- Does the animation start on the correct game event?
- Does the damage or destroy resolution happen at the intended frame?
- Does the final board state match authoritative game state?
- Do interrupts, skips, or rapid repeated triggers behave correctly?

## Visual Readability

- Is the hit frame obvious without replaying the clip?
- Can the player still identify attacker, target, and outcome?
- Does the motion support the action, or distract from it?
- Is there a single dominant accent at impact?

## Impact Quality

- Is there enough anticipation to make the strike readable?
- Is the hit-stop duration appropriate for the force level?
- Does the target react, or does the effect feel disconnected?
- Do screen feedback and flashes align with contact?

## Performance

- Does repeated triggering leak DOM nodes, canvases, or timers?
- Does frame pacing degrade under multiple consecutive effects?
- Are there avoidable layout thrashes, style recalculations, or large repaints?

## Acceptance Bar

Do not sign off if any of these are true:

- the player cannot tell exactly when contact happened
- destruction or damage feels visually detached from the hit
- the board becomes harder to parse after the effect
- the animation is only impressive in isolation and not inside real gameplay
