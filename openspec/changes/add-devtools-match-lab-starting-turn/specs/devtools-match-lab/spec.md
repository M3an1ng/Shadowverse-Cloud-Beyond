# add-devtools-match-lab-starting-turn

## ADDED Requirements

### Requirement: Devtools starting turn configuration
Match Lab MUST allow setting a starting turn number for both players, and the first main turn MUST begin with each player at that turn number.

#### Scenario: Developer starts a match at a later turn
- **WHEN** the user sets starting turn to 6 and starts a Match Lab game
- **THEN** the start-match API receives the starting turn value
- **AND** both players begin the first main turn with TurnNumber = 6
- **AND** evolution and super-evolution unlock timing follows that turn number

### Requirement: Starting turn validation
The devtools start-match API MUST validate the starting turn to be within 1 to the max-turns-before-draw limit.

#### Scenario: Invalid starting turn is rejected
- **WHEN** the user submits a starting turn outside the allowed range
- **THEN** the API rejects the request with a validation error
- **AND** the match is not started
