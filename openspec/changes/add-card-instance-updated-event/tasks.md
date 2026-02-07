## 1. Implementation
- [x] 1.1 Add CardInstanceUpdated domain event + DTO and wire EventType mapping.
- [x] 1.2 Track affected card instance IDs during a transaction and emit a single CardInstanceUpdated event at commit end.
- [x] 1.3 Update frontend handler to merge updated CardInstanceDto into local state and refresh preview.
- [x] 1.4 Add tests or scenario coverage for batched card updates after buff application.
