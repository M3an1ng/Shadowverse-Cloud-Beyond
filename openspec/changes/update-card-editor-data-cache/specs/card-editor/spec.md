## ADDED Requirements
### Requirement: Card Editor Local Cache
The card editor SHALL reuse locally cached card and ability data during a session and invalidate caches on local mutations.

#### Scenario: Switch cards without redundant queries
- **WHEN** the user navigates between cards or tabs that reuse the same data
- **THEN** the editor uses cached data when valid and avoids redundant requests

### Requirement: Deterministic Cache Invalidation
The card editor SHALL update or invalidate caches immediately after creating, editing, deleting, or copying abilities.

#### Scenario: Ability update reflects immediately
- **WHEN** the user updates an ability
- **THEN** cached data is updated/invalidated and the UI reflects the latest state

### Requirement: Backend Revision Token
The backend SHALL provide a cache revision token to allow clients to detect server-side data changes.

#### Scenario: Server data changes outside the client
- **WHEN** a client request receives a new revision token
- **THEN** the client invalidates stale caches and re-fetches data

### Requirement: Revision Validation on Requests
The card editor SHALL compare the revision token on each relevant card/ability response and clear local caches when the revision changes.

#### Scenario: Revision mismatch on response
- **WHEN** the client receives a response with a revision token different from the last seen value
- **THEN** it clears local caches and re-requests data as needed

### Requirement: Ability Count Batch Snapshot
The backend SHALL provide a batch snapshot endpoint for ability counts to reduce payload size for list views.

#### Scenario: Ability count display
- **WHEN** the card editor needs to show ability counts
- **THEN** it can fetch a batch snapshot of counts without loading full ability JSON
