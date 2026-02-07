## 1. Implementation
- [ ] 1.1 Define EventHistory query models and scope enums for DSL use.
- [ ] 1.2 Add a read-only EventHistory reader backed by IEventHistoryService with visibility filtering.
- [ ] 1.3 Expose the reader on GameContext as a DSL-accessible property.
- [ ] 1.4 Add DSL function `events(scope, filterExpr?, limit?)` that uses the reader.
- [ ] 1.5 Update DSL metadata/tests for event history querying.
