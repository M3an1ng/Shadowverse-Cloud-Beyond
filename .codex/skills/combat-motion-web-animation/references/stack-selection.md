# Stack Selection

Select the smallest stack that can deliver the required visual language.

## CSS Only

Use when the effect is mostly:

- translation
- scale
- rotation
- opacity
- blur or brightness flash
- simple card shake or recoil

Choose this first for card movement, hit flashes, and small target reactions.

## GSAP

Use when the interaction needs:

- a readable multi-phase timeline
- overlap between attacker, target, and FX elements
- deterministic orchestration across several DOM nodes
- precise easing and sequencing

Choose this first for attack sequences and combined hit effects in the browser.

## Canvas

Use when the effect needs:

- many sprites or particles
- controlled emission and decay
- a lighter-weight path than full WebGL

Choose this when DOM nodes become too expensive or awkward for burst FX.

## WebGL or Three.js

Use when the effect needs:

- dense particles
- shader-driven distortion
- additive blending stacks
- 3D camera language

Do not choose this for a single flash, shake, or card lunge.

## Rive or Lottie

Use when the effect is:

- lightweight
- state-driven
- mostly vector
- support UI rather than the main combat hit

Avoid using them as the main vehicle for combat-heavy impact FX.

## Selection Heuristics

- If the effect can be explained as a fixed sequence on a few DOM nodes, use GSAP.
- If the effect depends on dozens of spawned elements, prefer Canvas.
- If the effect depends on shaders, trails, or heavy additive particle fields, use WebGL.
- If the effect is decorative UI support, consider Rive or Lottie.
