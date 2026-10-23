## 1. Economy Reward Persistence

- [x] 1.1 Add dedicated community reward values to `CoinTransactionType` and update transaction display naming.
- [x] 1.2 Define stable `RelatedEntityId` formats for first-vote and first-comment rewards keyed by card definition.
- [x] 1.3 Add database migration for community reward idempotency so the same user, reward type, and card cannot be granted twice.

## 2. Community Backend Orchestration

- [x] 2.1 Introduce a `CardCommunityService` that encapsulates comment creation, vote mutation, reward evaluation, and response shaping.
- [x] 2.2 Move first-comment reward handling into the community service and keep comment creation plus reward grant within one transaction.
- [x] 2.3 Move first-vote reward handling into the community service and keep vote mutation plus reward grant within one transaction.
- [x] 2.4 Ensure reward eligibility is determined from persisted reward grant identity instead of current `CardComments` or `CardVotes` rows.
- [x] 2.5 Update `CardCommunityController` to call the community service and return reward metadata with community responses.

## 3. Frontend Reward Feedback

- [x] 3.1 Extend card community client handling to read reward metadata from comment and vote responses.
- [x] 3.2 Show immediate reward feedback in the card detail community UI for first rewarded actions.
- [x] 3.3 Refresh the visible player coin total after rewarded community actions without requiring a page reopen.

## 4. Verification

- [x] 4.1 Add backend tests for first-vote reward, first-comment reward, repeat actions, vote cancel, and comment deletion.
- [x] 4.2 Add backend tests for transaction rollback when reward persistence fails and for duplicate-request idempotency.
- [ ] 4.3 Manually verify card detail flows for rewarded and non-rewarded actions, including coin display refresh.
- [x] 4.4 Verify coin history and community responses expose the expected reward outcome and updated balance.
