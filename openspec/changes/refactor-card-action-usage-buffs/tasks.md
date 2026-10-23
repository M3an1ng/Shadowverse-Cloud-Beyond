## 1. Unified action usage state

- [x] 1.1 Add a fixed action-type model for `Attack`, `Act`, and `Fusion`
- [x] 1.2 Refactor `CardInstance` to store per-turn used counts through a unified internal structure
- [x] 1.3 Keep compatibility accessors for existing used-count properties and callers
- [x] 1.4 Update copy/reset/snapshot logic to copy and reset only used counts

## 2. Buff-derived usage limits

- [x] 2.1 Add a dedicated action-usage limit buff effect and related value model
- [x] 2.2 Extend `BuffService` and related factories to derive per-turn limits for supported action types
- [x] 2.3 Expose unified card-level APIs to read derived action limits and remaining quota

## 3. Action pipeline integration

- [x] 3.1 Update attack validation and consumption to use the unified action usage APIs
- [x] 3.2 Update Act request and commit flows to use the unified action usage APIs
- [x] 3.3 Update Fusion request and commit flows to use the unified action usage APIs
- [x] 3.4 Migrate `SetAttackTimes` to apply attack usage-limit buffs instead of directly mutating card state

## 4. Compatibility and verification

- [x] 4.1 Keep outward action-state events and mappings stable while sourcing values from the unified model
- [x] 4.2 Add or update tests for turn reset, buff expiry, suppression interaction, and multi-attack behavior
- [x] 4.3 Remove obsolete direct max-usage mutation paths after compatibility coverage is in place
