## ADDED Requirements
### Requirement: Account Registration
The system SHALL allow a guest user to register a new account using a username and password.

#### Scenario: Successful registration
- **WHEN** a guest submits a unique username and valid password
- **THEN** a new account is created and the session is linked to that account

#### Scenario: Duplicate username
- **WHEN** a guest submits a username that already exists
- **THEN** registration is rejected with a clear error

### Requirement: Account Login
The system SHALL allow a user to log in using a username and password.

#### Scenario: Successful login
- **WHEN** a user submits valid credentials
- **THEN** an authenticated session is established for that account

#### Scenario: Invalid credentials
- **WHEN** a user submits an invalid username or password
- **THEN** login is rejected with a clear error

### Requirement: Secure Credential Storage
The system SHALL store credentials only as salted secure password hashes.

#### Scenario: Registration stores a hash
- **WHEN** a user registers with a password
- **THEN** the stored credential is a hash and not the plaintext password

### Requirement: Cross-Server Account Persistence
The system SHALL persist account identity in shared storage so that login on any server maps to the same account id.

#### Scenario: Login on a different server
- **WHEN** a user logs in on another server instance with valid credentials
- **THEN** the system resolves to the same account id and loads the same data

### Requirement: Account-Bound Data
The system SHALL associate resources, decks, and match history with the account id.

#### Scenario: Load account data
- **WHEN** an authenticated user session starts
- **THEN** the system loads resources, decks, and match history for that account

### Requirement: Registration Entry UI
The client SHALL provide a registration entrypoint in the user-info-card.

#### Scenario: Access registration form
- **WHEN** a guest views the user-info-card
- **THEN** a registration button is available and opens a registration form
