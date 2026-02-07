## 1. Implementation
- [x] 1.1 Expand Before intercept eligibility to any event type and keep loop/ordering guards.
- [x] 1.2 Implement generic, safe parameter rewrite for `GameEventParameter` (Input/Both) properties.
- [x] 1.3 Generalize ReplaceEvent for leave-field conversions (Destroy/Return/Tuck) to Vanish.
- [x] 1.4 Remove the LeaveVanish keyword/system ability; use a card-configured Before ability with ReplaceEvent instead.
- [x] 1.5 Update tests (and card data if applicable) to use the configurable leave-field intercept ability.
- [x] 1.6 Add tests for generic rewrite, replace, and configurable leave-field interception.
- [ ] 1.7 Ensure ReplaceEvent preserves event identity for intercept-cycle gating (or gate by event ID if replacement changes it).
- [ ] 1.8 Align rewrite/replace failure handling with OnFailure (invalid rewrites should surface failure strategy).