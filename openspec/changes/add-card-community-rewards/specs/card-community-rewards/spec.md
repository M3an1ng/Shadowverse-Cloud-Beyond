## ADDED Requirements

### Requirement: First vote on a card grants coins once
The system SHALL grant `10` coins when a user successfully casts their first vote on a specific card definition. The reward SHALL be keyed by user, card definition, and vote-reward type, and SHALL be granted at most once per card regardless of whether the first vote is an upvote or downvote.

#### Scenario: First upvote grants reward
- **WHEN** a user without prior vote reward on card `X` successfully upvotes card `X`
- **THEN** the vote is stored
- **AND** the user receives `10` coins exactly once

#### Scenario: First downvote grants reward
- **WHEN** a user without prior vote reward on card `X` successfully downvotes card `X`
- **THEN** the vote is stored
- **AND** the user receives `10` coins exactly once

#### Scenario: Vote direction change does not grant second reward
- **WHEN** a user who already received the first-vote reward on card `X` changes their vote from upvote to downvote or from downvote to upvote
- **THEN** the vote state is updated
- **AND** no additional coins are granted

#### Scenario: Vote cancel does not restore reward eligibility
- **WHEN** a user who already received the first-vote reward on card `X` cancels the vote and later votes again on card `X`
- **THEN** the vote action succeeds according to community rules
- **AND** no additional coins are granted

### Requirement: First comment on a card grants coins once
The system SHALL grant `50` coins when a user successfully creates their first comment on a specific card definition. The reward SHALL be keyed by user, card definition, and comment-reward type, and SHALL be granted at most once per card.

#### Scenario: First comment grants reward
- **WHEN** a user without prior comment reward on card `X` creates their first valid comment on card `X`
- **THEN** the comment is stored
- **AND** the user receives `50` coins exactly once

#### Scenario: Later comments do not grant second reward
- **WHEN** a user who already received the first-comment reward on card `X` creates another valid comment on card `X`
- **THEN** the comment is stored
- **AND** no additional coins are granted

#### Scenario: Comment deletion does not restore reward eligibility
- **WHEN** a user deletes their only comment on card `X` after already receiving the first-comment reward
- **AND** later creates a new comment on card `X`
- **THEN** the new comment is stored
- **AND** no additional coins are granted

### Requirement: Reward eligibility shall be persisted independently from current community state
The system SHALL determine whether a community reward has already been granted from persisted reward identity, not by inferring from the current presence or absence of comments or votes in the community tables.

#### Scenario: Missing current vote does not imply reward eligibility
- **WHEN** a user previously received the first-vote reward on card `X` and currently has no active vote row for card `X`
- **THEN** the system still treats the reward as already consumed

#### Scenario: Missing current comment does not imply reward eligibility
- **WHEN** a user previously received the first-comment reward on card `X` and currently has no remaining comment row for card `X`
- **THEN** the system still treats the reward as already consumed

#### Scenario: Concurrent first action requests remain single-reward
- **WHEN** duplicate or concurrent first-action requests for the same user, card, and reward type are processed
- **THEN** at most one reward record is persisted
- **AND** at most one coin grant is applied

### Requirement: Community writes and reward grants shall be transactionally consistent
The system SHALL apply the community action and its corresponding first-time coin reward within the same transactional boundary so that partial success is not externally visible.

#### Scenario: Reward grant failure rolls back first comment
- **WHEN** a first-comment request qualifies for reward but the reward persistence cannot be completed
- **THEN** the comment creation is rolled back
- **AND** the user does not receive coins

#### Scenario: Reward grant failure rolls back first vote
- **WHEN** a first-vote request qualifies for reward but the reward persistence cannot be completed
- **THEN** the vote creation is rolled back
- **AND** the user does not receive coins

### Requirement: Community APIs report reward outcome to the client
The system SHALL return reward result metadata for community actions that can trigger first-time rewards so the client can update player-facing economy state immediately.

#### Scenario: First rewarded action returns reward metadata
- **WHEN** a user performs a first rewarded vote or first rewarded comment
- **THEN** the response includes that a reward was granted
- **AND** the response includes the granted coin amount
- **AND** the response includes the user's current coin balance after the transaction

#### Scenario: Non-rewarded action reports no reward
- **WHEN** a user performs a valid community action that does not qualify for a first-time reward
- **THEN** the response indicates that no reward was granted
