## 1. Implementation
- [x] 1.1 Introduce `ITriggerOrderingPolicy` (or equivalent service) and implement a default deterministic ordering policy.
- [x] 1.2 Integrate the policy into triggered ability ordering in `AbilityManager.GetTriggeredAbilitiesAsync` for all timings.
- [x] 1.3 Add and maintain `ZoneEnterSequence` on card instances when entering tracked zones.
- [x] 1.4 Ensure zone mutations keep `ZoneIndex` contiguous and deterministic across summon/move/return/transform paths.
- [x] 1.5 Preserve existing special priorities (attacker/defender/event source) while adding same-zone position ordering.
- [x] 1.6 Add unit/integration tests for same-zone ordering, insertion/reindex, tie-breakers, and deterministic replay behavior.
- [x] 1.7 Add diagnostics/logging of computed trigger order keys for debugging and regression triage.
