## ADDED Requirements

### Requirement: Activity events shall drive quest progress
The system SHALL accept standardized user activity events as the only progression input for activity-based quests. Supported activity types SHALL include match participation, match win, card comment, card vote, login, and match spectating.

#### Scenario: Comment activity progresses comment quests
- **WHEN** a persisted user successfully creates a valid card comment
- **THEN** the system records a comment activity event for that user
- **AND** active quests matching the comment activity type gain progress according to their progression rules

#### Scenario: Vote activity progresses vote quests
- **WHEN** a persisted user successfully creates or changes a card vote
- **THEN** the system records a vote activity event for that user
- **AND** active quests matching the vote activity type gain progress according to their progression rules

#### Scenario: Match settlement progresses match quests
- **WHEN** an official match is settled for a persisted non-AI participant
- **THEN** the system records a match participation activity event for that participant
- **AND** if the participant won, the system records a match win activity event for that participant

#### Scenario: Login activity progresses login quests
- **WHEN** a persisted user completes a login activity for the current period
- **THEN** the system records a login activity event for that user
- **AND** active quests matching the login activity type gain progress according to their progression rules

#### Scenario: Spectating activity progresses spectating quests
- **WHEN** a persisted user completes a valid spectating activity
- **THEN** the system records a spectating activity event for that user
- **AND** active quests matching the spectating activity type gain progress according to their progression rules

### Requirement: Quest progress shall be scoped to quest periods
The system SHALL scope recurring quest progress to an explicit quest period so that daily, weekly, special, and achievement quests do not overwrite each other's progress.

#### Scenario: Daily quest creates today's user quest
- **WHEN** a user performs an activity matching an active daily quest definition
- **THEN** the system creates or updates the user's quest record for that quest definition and the current daily period

#### Scenario: New day creates separate progress
- **WHEN** a user completed or progressed a daily quest yesterday and performs the matching activity today
- **THEN** the system uses a distinct user quest record for today's period
- **AND** yesterday's progress and reward state remain unchanged

#### Scenario: Achievement quest uses stable period
- **WHEN** a user performs an activity matching an active achievement quest definition
- **THEN** the system uses a stable non-recurring period for that achievement
- **AND** the achievement cannot be reset by daily or weekly period changes

### Requirement: Quest progress shall support deduplication policies
The system SHALL enforce each quest definition's deduplication policy before increasing progress. Deduplication SHALL be based on user, quest definition, period, source entity type, and source entity id.

#### Scenario: Same card vote counts once per period
- **WHEN** a daily vote quest uses per-card-per-period deduplication
- **AND** a user votes, cancels, changes direction, or votes again on the same card in the same period
- **THEN** the quest progress increases at most once for that card in that period

#### Scenario: Different card votes count separately
- **WHEN** a daily vote quest uses per-card-per-period deduplication
- **AND** a user successfully votes on two different cards in the same period
- **THEN** the quest progress may increase once for each distinct card

#### Scenario: Same match counts once for match quest
- **WHEN** a match quest uses per-match-per-period deduplication
- **AND** the same match settlement is processed more than once for a user
- **THEN** the quest progress increases at most once for that match and period

#### Scenario: Login counts once per period
- **WHEN** a login quest uses once-per-period deduplication
- **AND** a user logs in multiple times during the same period
- **THEN** the quest progress increases at most once for that period

### Requirement: Quest completion shall not automatically grant coins
The system SHALL mark a user quest completed when progress reaches the target value, but it SHALL NOT grant task reward coins during the progress update.

#### Scenario: Progress reaches target
- **WHEN** a user activity increases quest progress to the quest target value
- **THEN** the user quest is marked completed
- **AND** the user quest is marked unclaimed
- **AND** no quest reward coin transaction is created during progress update

#### Scenario: Completed quest ignores further progress
- **WHEN** a user quest is already completed for the current period
- **AND** the user performs additional matching activities
- **THEN** the quest remains completed
- **AND** current progress does not exceed the quest target value

### Requirement: Activity progress reporting shall identify updated and completed quests
The system SHALL report which quests were updated and which quests became completed after processing an activity event.

#### Scenario: Activity updates in-progress quest
- **WHEN** an activity event increases a quest that remains incomplete
- **THEN** the result identifies the updated quest
- **AND** the result does not identify it as newly completed

#### Scenario: Activity completes quest
- **WHEN** an activity event completes a quest
- **THEN** the result identifies the updated quest
- **AND** the result identifies the quest as newly completed

#### Scenario: Duplicate activity is ignored
- **WHEN** an activity event is rejected by the quest's deduplication policy
- **THEN** the result indicates that no progress was applied for that quest
