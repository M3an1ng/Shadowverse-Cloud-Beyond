# Change: Refactor Buff Refresh and Caching

## Why
High-frequency Buff evaluation with complex DSL conditions can become a bottleneck when a match has dozens of followers and thousands of Buffs. The current design evaluates ApplicabilityCondition during derived-state reads, which scales poorly and is hard to optimize with dependency tracking.

## What Changes
- Introduce revision-based derived-state caching for stats/keywords.
- Define explicit refresh points for conditional Buff evaluation (transaction end + turn boundaries).
- Centralize duration-based Buff expiry handling at turn boundaries.
- Add diagnostics for refresh cycles (opt-in).

## Impact
- Affected specs: buff-system (new capability).
- Affected code: Server/Domain/Buffs, Server/Domain/Events (TurnStart/TurnEnd), Server/Services/GameActionTransactionService.
