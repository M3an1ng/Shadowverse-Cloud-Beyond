## 1. Format Model and Persistence

- [x] 1.1 Add `DeckMode` enum and wire JSON serialization/deserialization for API contracts.
- [x] 1.2 Extend `UserDeck` with `Mode` and nullable `SecondaryClass`; update EF model configuration.
- [x] 1.3 Create migration to add `UserDecks.Mode` (default standard) and `UserDecks.SecondaryClass` (nullable).
- [x] 1.4 Extend deck DTOs and match deck selection DTOs to include `mode` and `secondaryClass`.

## 2. Validation and Rule Resolution

- [x] 2.1 Refactor deck validation input to include `(environment, mode, primaryClass, secondaryClass)`.
- [x] 2.2 Implement dual-class legality checks (allowed classes, primary<=24, secondary<=16, total size constraints).
- [x] 2.3 Implement neutral policy handling in dual-class mode (counts to total only).
- [x] 2.4 Return explicit validation errors for mode mismatch, missing secondary class, and quota violations.

## 3. Match Flow Consistency

- [x] 3.1 Extend `MatchDeckSelection` and related request handling to carry mode/secondary class context.
- [x] 3.2 Enforce same environment and mode between both sides before match start.
- [x] 3.3 Apply the same format consistency checks to AI fallback deck selection paths.
- [x] 3.4 Ensure runtime player state stores resolved environment and mode from validated selections.

## 4. Deck Code Compatibility

- [x] 4.1 Add versioned deck code encoding to include mode and secondary class metadata.
- [x] 4.2 Keep legacy deck code decoding path and map missing format fields to backward-compatible defaults.
- [x] 4.3 Validate decoded deck payloads against resolved `(environment, mode)` rules before save/import.

## 5. Frontend Deck Builder Contract

- [x] 5.1 Add mode selection and secondary class selection to deck builder state and payloads.
- [x] 5.2 Add dual-class quota indicators (`primary/24`, `secondary/16`, `total/40`) and disable invalid saves.
- [x] 5.3 Ensure standard mode remains backward-compatible and does not require secondary class.

## 6. Tests and Verification

- [x] 6.1 Add server tests for dual-class validation (quotas, off-class rejection, missing secondary class).
- [x] 6.2 Add integration tests for deck create/update/import across standard and dual-class modes.
- [x] 6.3 Add match flow tests for environment/mode mismatch rejection (PvP + AI fallback).
- [x] 6.4 Run impacted test suites and confirm no regression in existing deck management flows.
