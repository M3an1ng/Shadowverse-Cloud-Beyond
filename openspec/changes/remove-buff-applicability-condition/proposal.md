# Change: Remove Buff ApplicabilityCondition Evaluation

## Why
ApplicabilityCondition introduces per-buff evaluation overhead and duplicates the intended aura-based conditional buff behavior.

## What Changes
- Stop evaluating Buff.ApplicabilityCondition at runtime.
- Treat buff applicability as IsActive-only; conditional buffs should be implemented via Aura refresh.
- Retain the ApplicabilityCondition field for backward compatibility but mark it as unused.

## Impact
- Specs: buffs
- Code: BuffEvaluator, Buff documentation
