## 1. Resolver and Configuration Completion

- [x] 1.1 Add or update tests proving `PlayModeResolver` ignores Accelerate/Crystalize abilities without `AlternatePlay.PlayAsCardDefinitionId`.
- [x] 1.2 Add or update tests proving suppressed alternate play abilities are unavailable and granted effective alternate play abilities are available.
- [x] 1.3 Add or update tests proving same-cost Accelerate/Crystalize candidates select the earlier effective ability and return matching mode, runtime ID, cost, and replacement definition ID.
- [x] 1.4 Verify `AbilityConfig`, `IAbility`, runtime `Ability`, and system ability implementations expose alternate play metadata without changing non-alternate abilities.
- [x] 1.5 Verify server-side request validation rejects client-selected alternate ability runtime IDs that do not match resolver priority.

## 2. Replacement Definition Validation

- [x] 2.1 Add tests for Accelerate referencing a missing or non-Spell replacement definition.
- [x] 2.2 Add tests for Crystalize referencing a missing or non-Amulet replacement definition.
- [x] 2.3 Implement validation so invalid alternate replacement definitions fail before PP spend, zone movement, or card transformation.
- [x] 2.4 Add tests proving Base and Enhance play modes are unaffected by alternate replacement definition validation.

## 3. Alternate Play Preflight View

- [x] 3.1 Add a failing transaction preflight test for an Accelerate replacement spell with required target selection.
- [x] 3.2 Add a failing cancellation test proving alternate play target-selection cancellation leaves the card in hand with its original definition and does not spend PP.
- [x] 3.3 Create an alternate play context/helper that resolves the selected replacement definition and replacement ability configs from `PlayCardEvent.AlternateCardDefinitionId`.
- [x] 3.4 Update transaction primary ability analysis to use replacement play abilities for alternate `PlayCardEvent` preflight without globally transforming or registering the card.
- [x] 3.5 Ensure temporary replacement preflight abilities preserve the original card instance ID as source for target/effect context.
- [x] 3.6 Ensure preflight caches and cancellation cleanup do not leak temporary replacement abilities into later transactions.

## 4. Playability and Target Availability

- [x] 4.1 Add playability tests proving replacement card preconditions are evaluated for selected alternate play.
- [x] 4.2 Add playability tests proving target availability for alternate play uses replacement card abilities.
- [x] 4.3 Add playability tests proving a full field allows Accelerate spell play when all other requirements pass.
- [x] 4.4 Add playability tests proving a full field blocks Crystalize amulet play.
- [x] 4.5 Update `GameService` playability logic so selected alternate play uses replacement definition type, preconditions, ability configs, and target availability.
- [x] 4.6 Verify client-side play guards match server behavior for Accelerate and Crystalize field-space checks.

## 5. Commit, Events, and Zone Results

- [ ] 5.1 Add commit tests proving successful Accelerate transforms the same card instance to the replacement Spell before spell movement.
- [ ] 5.2 Add commit tests proving successful Crystalize transforms the same card instance to the replacement Amulet before field placement.
- [ ] 5.3 Add tests proving Accelerate and Crystalize events are emitted only after successful commit and include player, card, paid cost, and triggering ability runtime ID.
- [ ] 5.4 Add tests proving failed alternate play emits no Accelerate/Crystalize success event.
- [ ] 5.5 Verify transform cleanup, ability re-registration, buff invalidation, and zone movement run in the correct order for alternate play.

## 6. Replay, Logs, and DTO Projection

- [ ] 6.1 Add tests proving event history and match-history projections observe the replacement definition after successful Accelerate.
- [ ] 6.2 Add tests proving field projections observe the replacement definition after successful Crystalize.
- [ ] 6.3 Add DTO/log conversion support for Accelerate and Crystalize events if existing conversion paths do not surface them.
- [ ] 6.4 Decide and implement log display text for original card name versus replacement card name according to the design open question.

## 7. Ability Editor and Data Authoring

- [ ] 7.1 Add editor tests for configuring Accelerate with a Spell replacement card definition.
- [ ] 7.2 Add editor tests for configuring Crystalize with an Amulet replacement card definition.
- [ ] 7.3 Add editor validation that warns or blocks Accelerate replacement definitions that are not Spell cards.
- [ ] 7.4 Add editor validation that warns or blocks Crystalize replacement definitions that are not Amulet cards.
- [ ] 7.5 Update the ability editor UI so declaration abilities clearly show replacement-card metadata and do not imply their own normal targets/effects will execute.
- [ ] 7.6 Update metadata/category definitions if needed so alternate play configuration is discoverable in the editor.

## 8. Optional Availability Conditions

- [ ] 8.1 Decide whether `AlternatePlay.AvailabilityConditions` are in scope for this implementation pass.
- [ ] 8.2 If in scope, add resolver/playability tests proving unmet availability conditions hide or disable that alternate play candidate.
- [ ] 8.3 If in scope, implement condition evaluation for alternate play availability using the existing condition evaluator.
- [ ] 8.4 If out of scope, document that `AvailabilityConditions` are reserved metadata and add a test or validation preventing silent reliance on them.

## 9. Regression and Verification

- [ ] 9.1 Run targeted backend tests for play mode resolution, play requests, transaction preflight, playability, commit, and event projection.
- [ ] 9.2 Run frontend tests or `npm run build` to verify play guards and editor changes compile.
- [ ] 9.3 Run `openspec status --change "add-alternate-play-modes"` and confirm all artifacts remain complete.
- [ ] 9.4 Run `openspec verify --change "add-alternate-play-modes"` or the project-equivalent OpenSpec validation command if available.
- [ ] 9.5 Record any unrelated full-suite failures separately from alternate-play implementation failures.
