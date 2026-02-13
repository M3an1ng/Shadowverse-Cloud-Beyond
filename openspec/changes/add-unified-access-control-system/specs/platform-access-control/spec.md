## ADDED Requirements
### Requirement: Unified server-side identity verification
The system SHALL validate caller identity on the server side for all protected HTTP APIs and SHALL NOT trust client-submitted `userId` or `actorUserId` as authentication evidence.

#### Scenario: Protected API call with valid token
- **WHEN** a client calls a protected API with a valid access token
- **THEN** the server resolves the caller identity from token claims and continues authorization

#### Scenario: Forged userId without token
- **WHEN** a client sends a protected request containing another user's `userId` but no valid token
- **THEN** the request is rejected as unauthorized

### Requirement: Unified SignalR authentication
The system SHALL authenticate SignalR connections and method invocations using the same identity model as HTTP APIs.

#### Scenario: Hub connection with valid identity
- **WHEN** a client connects to SignalR using a valid token
- **THEN** the connection is bound to the authenticated user identity

#### Scenario: Hub invocation after token invalidation
- **WHEN** an authenticated token is revoked or expired
- **THEN** subsequent protected hub invocations are denied according to policy

### Requirement: Token lifecycle and revocation
The system SHALL support short-lived access tokens, refresh token rotation, and server-side token revocation.

#### Scenario: Access token expiration
- **WHEN** an access token expires
- **THEN** protected requests fail with an authentication error until a valid token is refreshed

#### Scenario: Refresh token rotation
- **WHEN** a client refreshes a session successfully
- **THEN** the previous refresh token is invalidated and a new token pair is issued

### Requirement: Role and permission model
The system SHALL provide a role-based permission model with explicit permission levels and permission codes for privileged actions.

#### Scenario: Default player permissions
- **WHEN** a newly registered user signs in
- **THEN** the user receives the default player role and only its granted permissions

#### Scenario: Admin permission grant
- **WHEN** an administrator role is assigned to a user
- **THEN** admin-scoped policies can be evaluated as allowed for that user

### Requirement: Policy-based authorization with default deny
The system SHALL enforce authorization through named policies and SHALL deny access by default for protected operations without a satisfied policy.

#### Scenario: Missing required policy
- **WHEN** a caller is authenticated but does not satisfy the required policy for an operation
- **THEN** the operation is rejected with forbidden status

#### Scenario: Protected operation with satisfied policy
- **WHEN** a caller satisfies the operation policy
- **THEN** the operation is permitted

### Requirement: Resource ownership authorization
The system SHALL enforce owner-or-admin checks for user-bound resources (for example decks, economy data, user profile, and devtools private state).

#### Scenario: Owner accesses own resource
- **WHEN** an authenticated user requests a resource they own
- **THEN** access is allowed if the action policy allows owner access

#### Scenario: Non-owner access without admin privilege
- **WHEN** a user requests another user's protected resource without admin privilege
- **THEN** access is denied

### Requirement: Admin user management safety constraints
The system SHALL enforce safety constraints for privileged user-management operations.

#### Scenario: Last admin demotion protection
- **WHEN** an admin attempts to demote the final active administrator account
- **THEN** the operation is rejected with a clear validation error

#### Scenario: Unauthorized role update attempt
- **WHEN** a non-admin user attempts to change another user's role
- **THEN** the operation is rejected as forbidden

### Requirement: Authorization auditability
The system SHALL emit structured audit records for privileged operations and authorization denials.

#### Scenario: Privileged role change auditing
- **WHEN** a role assignment operation is attempted
- **THEN** an audit record is written with actor, target, policy, decision, and timestamp

#### Scenario: Denied sensitive access auditing
- **WHEN** access to a sensitive operation is denied by policy
- **THEN** the denial reason and identity context are recorded for investigation

### Requirement: Frontend permission projection contract
The system SHALL provide a frontend permission projection contract for UI rendering, while the backend remains the final authorization authority.

#### Scenario: Frontend hides admin UI for non-admin users
- **WHEN** the session projection indicates no admin permission
- **THEN** admin-only UI entry points are hidden or disabled

#### Scenario: Backend rejects forged admin UI request
- **WHEN** a client manually invokes an admin endpoint despite hidden UI restrictions
- **THEN** backend policy enforcement still denies the operation
