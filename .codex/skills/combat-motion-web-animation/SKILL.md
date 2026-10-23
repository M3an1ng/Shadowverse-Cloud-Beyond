---
name: combat-motion-web-animation
description: Design and implement combat animation and hit effects for browser-based games from reference through QA. Use when Codex needs to add or refine attack motion, hit-stop, impact flashes, slash trails, screen shake, card movement, combat UI motion, or other battle feedback in web games or interactive card interfaces, especially when the task requires collecting visual references, translating them into a motion brief, selecting a web animation stack, building a prototype, integrating it into a real game flow, and validating timing, readability, and performance.
---

# Combat Motion Web Animation

## Overview

Turn combat animation work into a strict pipeline: gather references, extract motion language, choose the smallest viable web stack, prototype the sequence, integrate it into the real interaction, and verify functional plus visual quality.

Prefer motion that improves combat readability and impact over motion that only adds spectacle.

## Quick Start

1. Identify the interaction type and read [references/combat-motion-taxonomy.md](references/combat-motion-taxonomy.md).
2. Gather 3-5 references with [references/source-sites.md](references/source-sites.md).
3. Build a concrete brief with `python scripts/build_motion_brief.py --feature "<feature>" --pattern <pattern>`.
4. Choose the lightest implementation path with [references/stack-selection.md](references/stack-selection.md).
5. Prototype timing with placeholders before adding final art.
6. Integrate into the real game event flow instead of a disconnected demo.
7. Generate and run a QA checklist with `python scripts/build_animation_qa.py --feature "<feature>" --pattern <pattern>`.

## Decision Rules

- If the user only wants inspiration or direction, read [references/source-sites.md](references/source-sites.md) and [references/combat-motion-taxonomy.md](references/combat-motion-taxonomy.md).
- If the user already knows the desired feel but not the implementation path, read [references/stack-selection.md](references/stack-selection.md).
- If the user asks to implement or refine a specific combat interaction, run `build_motion_brief.py`, then implement, then run `build_animation_qa.py`.
- If the motion already exists but feels weak, read [references/review-checklist.md](references/review-checklist.md) first and diagnose the missing phase, missing contrast, or missing feedback layer before rewriting code.

## Workflow

## 1. Classify the interaction

Choose the closest pattern before writing code:

- `combat-attack`: windup, travel, impact, recovery
- `ability-cast`: charge, release, payload, resolution
- `destroy-resolution`: target callout, fatal hit, vanish, board settle
- `card-entry`: spawn, focus, settle

If none fit exactly, pick the nearest pattern and adapt it. Do not invent a new pattern unless the existing ones fail.

## 2. Gather references

Collect references across three buckets:

- gameplay VFX reference
- motion direction reference
- implementation reference

Use [references/source-sites.md](references/source-sites.md) and capture only the useful parts:

- path of motion
- impact frame treatment
- camera or screen feedback
- layering order
- duration estimate

Avoid vague notes such as "more punchy" or "more premium." Convert them into observable behavior.

## 3. Produce a motion brief

Use `scripts/build_motion_brief.py` to create a first-pass brief. Review and adjust it against the actual UI constraints.

The brief must answer:

- What is the player action?
- What should feel fast, heavy, magical, brutal, or precise?
- Which frame is the hit frame?
- Which layers are essential: movement, flash, shake, particles, deformation, sound hook?
- What is the maximum acceptable duration before the interaction feels sluggish?

If the feature is combat-critical, keep the first prototype under one second total unless the design explicitly requires a long cast.

## 4. Select the stack

Read [references/stack-selection.md](references/stack-selection.md) and prefer the smallest stack that can express the effect:

- CSS transforms for simple translation, scale, opacity, blur, and filter flashes
- GSAP for sequenced timelines and synchronized multi-element motion
- Canvas or WebGL for particle-heavy or shader-like effects
- Rive or Lottie only for lightweight supporting animation, not the main combat hit effect

Do not jump to WebGL because it sounds advanced. Use it only when DOM-based motion cannot deliver the result at the required density or performance.

## 5. Prototype the sequence

Prototype with placeholder cards, blocks, or simple shapes first.

Lock these items before polishing:

- duration of each phase
- hit-stop timing
- overlap between attacker, target, and impact FX
- board readability during the effect

If the timing feels wrong with placeholders, final art will not save it.

## 6. Integrate into the real event flow

Attach animation to the actual game state change path. The implementation should know:

- when the action starts
- when damage or destruction resolves
- when the target disappears or changes state
- when control returns to the player

Do not let visuals race ahead of authoritative state changes without an explicit design reason.

## 7. Verify and review

Read [references/review-checklist.md](references/review-checklist.md) and run `scripts/build_animation_qa.py`.

Always verify:

- functional sequencing
- visual readability
- performance on repeated triggers
- interruption or skip behavior
- final board state correctness

## Defaults

- Prefer short, strong motion over long ornamental motion.
- Prefer one clear impact accent over many weak accents.
- Prefer additive feedback layers that each have a job.
- Prefer deterministic timelines over scattered ad hoc timers.
- Prefer explicit durations and named phases over "tweak until it looks right."

## Bundled Resources

- `references/source-sites.md`: reference sources and search strategies
- `references/combat-motion-taxonomy.md`: shared motion language and phase definitions
- `references/timing-patterns.md`: timing presets and pacing ranges
- `references/stack-selection.md`: implementation stack decision rules
- `references/review-checklist.md`: signoff criteria
- `scripts/build_motion_brief.py`: generate a structured brief from a feature and pattern
- `scripts/build_animation_qa.py`: generate a QA checklist for a combat interaction
- `assets/motion-brief-template.md`: reusable brief template
- `assets/storyboard-template.md`: reusable storyboard template
