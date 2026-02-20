## ADDED Requirements
### Requirement: Admin Can Reset User Password
The system SHALL allow an admin to reset a target user's password from user management.

#### Scenario: Admin resets password successfully
- **WHEN** an active admin submits a valid new password for an existing user
- **THEN** the server updates the user's stored password hash
- **AND** the server returns success with the target user identifier

#### Scenario: Non-admin attempts password reset
- **WHEN** a non-admin user submits a reset request
- **THEN** the server rejects the request with a forbidden response

#### Scenario: New password does not meet minimum length
- **WHEN** an admin submits a new password shorter than the minimum length
- **THEN** the server rejects the request with a bad-request response
