---
name: checkpoint-code-map
description: Build and refresh a compact repository code map at checkpoints, then use map lookups to navigate files and symbols with minimal exploratory searches. Use when working in medium/large codebases, when requests involve cross-file changes, or when repeated discovery is wasting context budget.
---

# Checkpoint Code Map

Use this skill to keep an up-to-date index of file structure and symbols, then query the index before broad `rg` exploration.

## Workflow

1. Build or refresh map at a checkpoint:
```bash
python .codex/skills/checkpoint-code-map/scripts/code_map.py build \
  --root . \
  --out .codex/code-map.json \
  --summary .codex/code-map.md \
  --changed-only
```
2. Query symbol first:
```bash
python .codex/skills/checkpoint-code-map/scripts/code_map.py lookup \
  --map .codex/code-map.json \
  --symbol CardBurnedEvent
```
3. Query by path/topic before full-text search:
```bash
python .codex/skills/checkpoint-code-map/scripts/code_map.py lookup \
  --map .codex/code-map.json \
  --path PlayerState
```

## Operating Rules

- Refresh map at every meaningful checkpoint: after finishing a feature slice, bugfix, or refactor step.
- Prefer `--changed-only` for frequent updates; run a full build after large renames/moves.
- Read `.codex/code-map.md` for quick architecture orientation before opening many files.
- Use lookup results to narrow file targets, then run precise `rg` against the shortlisted files.
- If lookup misses expected symbols, rebuild without `--changed-only`.

## Output Contract

- `code-map.json` stores reusable machine-readable index.
- `code-map.md` stores compact human summary for fast scan in context.
- Keep both files in `.codex/` so they are stable across sessions.
