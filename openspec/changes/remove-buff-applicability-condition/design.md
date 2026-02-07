## Context
Buff.ApplicabilityCondition currently gates whether a buff contributes to derived stats. This adds per-buff condition evaluation cost on refresh cycles and overlaps with the planned aura-based conditional buff system.

## Goals / Non-Goals
- Goals:
  - Disable ApplicabilityCondition evaluation in buff applicability checks.
  - Standardize conditional buffs to use Aura refresh instead.
- Non-Goals:
  - Removing the ApplicabilityCondition field from data models.
  - Changing buff stacking or duration logic.

## Decisions
- BuffEvaluator will only check IsActive; ApplicabilityCondition is ignored.
- ApplicabilityCondition field remains but is considered deprecated/unused.

## Risks / Trade-offs
- Existing content using ApplicabilityCondition will become always-on (if IsActive).
- Requires migrating conditional buffs to aura-based implementation to preserve behavior.

## Migration Plan
- Update conditional buff usage to Aura configs before enabling in production.
- Add notes in documentation for deprecation of ApplicabilityCondition.
