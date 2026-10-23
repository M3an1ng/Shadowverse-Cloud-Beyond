## ADDED Requirements

### Requirement: Activity quest rewards shall be claimed explicitly
The system SHALL grant activity quest reward coins only when a user explicitly claims a completed, unclaimed, non-expired user quest.

#### Scenario: Claim completed quest reward
- **WHEN** a persisted user claims a completed, unclaimed, non-expired activity quest
- **THEN** the system marks the user quest reward as claimed
- **AND** the system creates exactly one quest completion coin transaction for the quest reward amount
- **AND** the user's coin balance increases by the quest reward amount

#### Scenario: Claim incomplete quest reward is rejected
- **WHEN** a persisted user claims an incomplete activity quest
- **THEN** the system rejects the claim
- **AND** no coin transaction is created
- **AND** the user's coin balance does not change

#### Scenario: Claim already claimed quest reward is rejected
- **WHEN** a persisted user claims an activity quest whose reward was already claimed
- **THEN** the system rejects the claim
- **AND** no additional coin transaction is created
- **AND** the user's coin balance does not change

#### Scenario: Claim expired quest reward is rejected
- **WHEN** a persisted user claims an expired recurring activity quest
- **THEN** the system rejects the claim
- **AND** no coin transaction is created
- **AND** the user's coin balance does not change

### Requirement: Non-match activity shall not directly grant coins
The system SHALL NOT directly grant coins for card comments, card votes, login activities, or spectating activities. These activities SHALL only affect coins by completing quests that are later claimed.

#### Scenario: Card vote does not directly grant coins
- **WHEN** a persisted user successfully votes on a card
- **THEN** the system may update matching quest progress
- **AND** the system does not create a card community vote reward coin transaction
- **AND** the user's coin balance does not change until a completed quest reward is claimed

#### Scenario: Card comment does not directly grant coins
- **WHEN** a persisted user successfully creates a card comment
- **THEN** the system may update matching quest progress
- **AND** the system does not create a card community comment reward coin transaction
- **AND** the user's coin balance does not change until a completed quest reward is claimed

#### Scenario: Login does not directly grant activity quest coins
- **WHEN** a persisted user completes a login activity
- **THEN** the system may update matching quest progress
- **AND** the login activity does not directly create a quest reward coin transaction

#### Scenario: Spectating does not directly grant coins
- **WHEN** a persisted user completes a valid spectating activity
- **THEN** the system may update matching quest progress
- **AND** the user's coin balance does not change until a completed quest reward is claimed

### Requirement: Official match settlement coin rewards shall remain direct
The system SHALL preserve direct official match settlement coin rewards independently from activity quest rewards.

#### Scenario: Official match grants settlement coins and progresses quests
- **WHEN** an official match is settled for a persisted non-AI participant
- **THEN** the system grants the participant the configured match settlement coins
- **AND** the system records match activity for matching quests
- **AND** any completed quest remains unclaimed until the user claims its reward

#### Scenario: Non-official match does not grant settlement coins
- **WHEN** a non-official match is settled for a persisted non-AI participant
- **THEN** the system does not grant direct match settlement coins
- **AND** quest progress follows the configured eligibility rules for match activities

### Requirement: Quest reward transactions shall be idempotent
The system SHALL prevent duplicate quest reward coin transactions for the same user quest.

#### Scenario: Duplicate claim request grants once
- **WHEN** duplicate claim requests are processed for the same completed user quest
- **THEN** at most one request succeeds
- **AND** exactly one quest completion coin transaction exists for that user quest
- **AND** the user's coin balance increases only once

#### Scenario: Claim failure does not leave partial reward state
- **WHEN** a quest reward claim cannot persist the coin transaction
- **THEN** the user quest remains unclaimed
- **AND** the user's coin balance does not change

### Requirement: Activity responses shall report quest progress instead of direct coin reward
The system SHALL expose activity quest progress metadata for user actions that can progress quests, and SHALL NOT report non-match activity as direct coin reward.

#### Scenario: Community vote response includes quest progress metadata
- **WHEN** a card vote updates or completes matching quests
- **THEN** the response includes quest progress metadata for the action
- **AND** the response does not indicate that vote coins were directly granted

#### Scenario: Community comment response includes quest progress metadata
- **WHEN** a card comment updates or completes matching quests
- **THEN** the response includes quest progress metadata for the action
- **AND** the response does not indicate that comment coins were directly granted

#### Scenario: Claim response includes updated coin balance
- **WHEN** a user successfully claims an activity quest reward
- **THEN** the response includes the claimed reward amount
- **AND** the response includes the user's coin balance after the claim
