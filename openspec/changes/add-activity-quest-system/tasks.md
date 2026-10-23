## 1. Domain Model and Persistence

- [x] 1.1 Add `QuestActivityType` and `QuestDeduplicationPolicy` enums with values for match participation, match win, comment, vote, login, and spectating.
- [x] 1.2 Extend `QuestDefinition` with activity type and deduplication policy fields while preserving existing quest display fields.
- [x] 1.3 Extend `UserQuest` with `PeriodKey`, `PeriodStartUtc`, and `PeriodEndUtc`.
- [x] 1.4 Add `QuestProgressEvent` entity for period-scoped activity deduplication.
- [x] 1.5 Update `ApplicationDbContext` mappings for quest definition fields, periodized user quest uniqueness, and quest progress event uniqueness.
- [x] 1.6 Add EF migration for quest activity fields, user quest period fields, quest progress events, and migrated uniqueness constraints.
- [x] 1.7 Seed or migrate existing quest definitions to activity types and deduplication policies.

## 2. Quest Progress Service

- [x] 2.1 Add `IQuestProgressService` and result DTOs for updated quests, completed quests, and duplicate/no-op outcomes.
- [x] 2.2 Implement period resolution for daily, weekly, special, and achievement quests.
- [x] 2.3 Implement matching active quest definitions by `QuestActivityType`.
- [x] 2.4 Implement user quest create-or-load by user, quest definition, and period key.
- [x] 2.5 Implement deduplication using `QuestProgressEvent` before applying progress.
- [x] 2.6 Implement progress updates that mark quests completed without granting coins.
- [x] 2.7 Register `IQuestProgressService` in dependency injection.

## 3. Existing Service Integration

- [x] 3.1 Update `MetaProgressionService` to record match participation and win activity events after official match settlement while preserving direct match coin rewards.
- [x] 3.2 Update `CardCommunityService` comment flow to record comment activity instead of granting direct community comment coins.
- [x] 3.3 Update `CardCommunityService` vote flow to record vote activity instead of granting direct community vote coins.
- [x] 3.4 Preserve `CoinTransactionType.CardCommunityFirstVote` and `CardCommunityFirstComment` display compatibility for historical coin history.
- [x] 3.5 Add login activity recording at the selected login/session entry point.
- [x] 3.6 Add spectating activity recording at the selected valid spectating completion point.
- [x] 3.7 Keep legacy `EconomyService.UpdateQuestProgressAsync` compatible or route it through the new activity progress service.

## 4. Rewards and APIs

- [x] 4.1 Ensure quest reward claims remain explicit and use `CoinTransactionType.QuestCompletion`.
- [x] 4.2 Make quest reward claims idempotent for a user quest under duplicate requests.
- [x] 4.3 Extend quest claim response with current coin balance after successful claim.
- [x] 4.4 Add quest progress metadata DTOs for activity responses.
- [x] 4.5 Update card comment and vote responses to return quest progress metadata instead of direct community reward metadata.
- [x] 4.6 Update meta progression state output if needed to expose claimable quest counts or completed quest summaries.

## 5. Frontend Updates

- [x] 5.1 Replace card community direct coin reward handling with quest progress and quest completion feedback.
- [x] 5.2 Stop refreshing top coin balance after comment/vote unless a quest reward is claimed.
- [x] 5.3 Update the meta progression panel to show updated/completed/claimable activity quests clearly.
- [x] 5.4 Refresh the top coin balance after successful task reward claim.
- [x] 5.5 Remove or repurpose community reward animation text that says first vote/comment grants coins.

## 6. Tests and Verification

- [x] 6.1 Add backend tests for daily vote progress with same-card deduplication and different-card counting.
- [x] 6.2 Add backend tests for comment activity progress and selected comment deduplication behavior.
- [x] 6.3 Add backend tests for daily login once-per-period progress.
- [x] 6.4 Add backend tests for spectating activity progress and duplicate match suppression.
- [x] 6.5 Add backend tests for official match settlement preserving direct coins while completing match quests without auto-claiming rewards.
- [x] 6.6 Update existing card community reward tests to expect quest progress and no direct community coin transactions.
- [x] 6.7 Add backend tests for periodized user quests across two daily periods.
- [x] 6.8 Add backend tests for duplicate quest reward claim idempotency and rollback behavior.
- [x] 6.9 Run relevant backend tests with `dotnet test` and record any remaining manual verification gaps.
- [ ] 6.10 Manually verify card detail community flows, task completion prompts, task claiming, and coin balance refresh in the browser.
