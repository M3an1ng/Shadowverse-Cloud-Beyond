## Context
We now have complete card data for all crafts in `data/card_data_*.json`. The scenario harness exists, but only 일부 craft 有场景覆盖。需要制定每张卡的可执行场景并逐一实现。

## Goals / Non-Goals
- Goals: 每张卡至少一个可执行场景；场景覆盖实际触发路径（Fanfare/LastWords/Act/TurnStart 等）。
- Non-Goals: 不在此变更中重新设计 DSL 或能力系统。

## Decisions
- Decision: Use a generated scenario catalog to seed per-card tests, with manual overrides for cards needing extra setup.
- Alternatives considered: Hand-written tests only (rejected due to scale).

## Risks / Trade-offs
- Risk: Some cards require complex setup (tokens, counters, transformed cards).
  - Mitigation: Mark catalog entries with required setup tags and handle them first.

## Migration Plan
1. Produce scenario catalog.
2. Implement scenarios craft-by-craft.
3. Run scenario test suite and fix failures.

## Open Questions
- Which cards need multi-step scenarios (e.g., chained effects, Before intercepts)?
