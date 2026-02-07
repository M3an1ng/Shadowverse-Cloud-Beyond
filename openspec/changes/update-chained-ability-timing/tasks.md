## 1. Implementation
- [ ] 1.1 Reorder ProcessEffectAsync to process effectResult.GeneratedEvents before ProcessChainedAbilitiesAsync.
- [ ] 1.2 Compute final effect outcome from handler success plus generated-event processing result.
- [ ] 1.3 Move FailureHandling branching to after generated-event processing and gate chained abilities accordingly.
- [ ] 1.4 Update logs/diagnostics to reflect the new ordering.
- [ ] 1.5 Add tests covering chained timing after commit and failure gating behavior.
