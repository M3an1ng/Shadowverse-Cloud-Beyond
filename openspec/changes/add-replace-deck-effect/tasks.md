## 1. Domain Model And Metadata

- [x] 1.1 Add `EffectType.ReplaceDeck` and `ReplaceDeckEffectParams` with `PlayerScope` and ordered `CardDefinitionIds`.
- [x] 1.2 Add a dedicated `CardMoveReason` for deck replacement removal and register the new effect in effect UI metadata.

## 2. Core Implementation

- [x] 2.1 Implement `ReplaceDeckEffectHandler` to rebuild each targeted player's deck from the provided definition ID list.
- [x] 2.2 Move all replaced deck cards from `Deck` to `Removed` via `CardMovedEvent` without emitting `Vanish` semantics.
- [x] 2.3 Create fresh card instances for the replacement deck in the exact input order and fail fast on invalid replacement lists.

## 3. Verification

- [x] 3.1 Add tests for self/opponent or multi-player scope replacement behavior and resulting deck order.
- [x] 3.2 Add semantic tests proving replaced cards use the new move reason, end in `Removed`, and do not emit `CardVanishedEvent`.
