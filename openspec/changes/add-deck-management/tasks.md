## 1. Documentation
- [x] 1.1 Define deck-management requirements in spec
- [x] 1.2 Define match-deck-selection requirements in spec
- [x] 1.3 Capture design decisions and open questions

## 2. Data Model & Persistence
- [x] 2.1 Add UserDeck and UserDeckCard entities with UserId ownership
- [x] 2.2 Configure EF Core mappings and indexes
- [x] 2.3 Add database migrations for new tables

## 3. Services & APIs
- [x] 3.1 Implement deck validation rules (size 40, max 3 copies, class restrictions)
- [x] 3.2 Implement deck CRUD service and controller endpoints
- [x] 3.3 Implement deck code encode/decode service and endpoints
- [ ] 3.4 Add user card collection read endpoint if required by editor
- [x] 3.5 Implement default deck bootstrap for users/guests
- [x] 3.6 Add deck availability check endpoint for match entry gating

## 4. Match Integration
- [x] 4.1 Extend match requests to include deck selections (host, guest, AI)
- [x] 4.2 Persist selections in MatchRoom and validate ownership
- [x] 4.3 Build GameState decks from selected deck lists

## 5. Tests
- [x] 5.1 Unit tests for deck validation and deck code round-trip
- [ ] 5.2 Integration tests for deck CRUD and match start with deck selection

## 6. Deck Environment & Card Pool
- [x] 6.1 Add deck environment enum and persist it on deck entities (including guest store + migration)
- [x] 6.2 Require environment on deck creation and default it for bootstrap/import flows
- [x] 6.3 Add environment + class selection step before opening deck editor
- [x] 6.4 Filter deck editor card pool by environment rules and deck class
