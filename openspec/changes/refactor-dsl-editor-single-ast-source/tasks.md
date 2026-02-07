## 1. Implementation
- [ ] 1.1 Inventory AST entry points (main edit, workspace edit, save, unnest, undo).
- [ ] 1.2 Store a single mutable AST in `workspaceAST` and derive `astRoot` from it.
- [ ] 1.3 Update save/unnest/ascend flows to use the workspace AST even when `editingPath` is empty.
- [ ] 1.4 Align validation/render entry points to read from the single AST source.
- [ ] 1.5 Add guards/logging for divergence and cover save-after-unnest with tests.
