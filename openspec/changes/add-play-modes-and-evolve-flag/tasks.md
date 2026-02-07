## 1. Implementation
- [x] 1.1 Define PlayMode enum and DTO shape for play mode availability (mode, effectiveCost, isAvailable, reason).
- [x] 1.2 Extend PlayCardActionDto and PlayCardEvent to carry the selected PlayMode and paid cost.
- [x] 1.3 Update playability checks to compute available modes, choose default mode (Enhance > Base), and return mode info in PlayabilityResultDto.
- [x] 1.4 Add a mode-disabling rule hook (e.g., EnhanceDisabled) and ensure the mode is removed from availability.
- [x] 1.5 Add IsSuperEvolution to EvolveEvent and set it in the super evolve flow.
- [x] 1.6 Expose new event fields to DSL metadata and ensure conditions can branch on IsSuperEvolution and PlayMode.
- [x] 1.7 Update client event/DTO mapping and UI consumption for play mode info.
- [x] 1.8 Add tests for mode selection, Enhance-disabled behavior, and evolve/super evolve branching.
