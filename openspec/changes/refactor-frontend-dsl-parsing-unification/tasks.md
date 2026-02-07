## 1. Implementation
- [ ] 1.1 Inventory frontend DSL parsing entry points (editor render, validation, local parse) and define the single pipeline contract.
- [ ] 1.2 Refactor local parsing (`ast-parse.js`) to delegate to the shared AST mapping/type inference rules.
- [ ] 1.3 Align validation and preview flows to use the unified pipeline and pass triggering-event context.
- [ ] 1.4 Update related UI editors that call validation to use the same options/context injection.
- [ ] 1.5 Add or update frontend tests that assert consistent type resolution across editor and validation paths.
