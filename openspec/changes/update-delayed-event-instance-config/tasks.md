## 1. Implementation
- [x] 1.1 Define DelayConfig and update IDelayedEvent to expose instance-scoped delay configuration.
- [x] 1.2 Update GameActionTransactionService to use DelayConfig and enforce default disabled behavior.
- [x] 1.3 Migrate existing delayed events and creation sites to set DelayConfig explicitly (preserve current semantics).
- [x] 1.4 Add validation/guardrails for delay config mutation after enqueue (or document allowed timing).
- [x] 1.5 Add/update tests or log assertions to verify ordering stays consistent.

