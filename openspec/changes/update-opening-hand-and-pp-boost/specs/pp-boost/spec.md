## ADDED Requirements
### Requirement: Second player PP+1 availability windows
The system SHALL grant the second player a PP+1 boost ability with 1 available use at match start. At the start of the second player's turn 6, the available use SHALL reset to 1 regardless of whether the first use was spent. The total number of uses across the match SHALL NOT exceed 2.

#### Scenario: Initial availability
- **WHEN** the match starts
- **THEN** the second player has 1 available PP+1 use and the first player has 0

#### Scenario: Reset after use
- **GIVEN** the second player has used PP+1 before turn 6
- **WHEN** the second player's turn number becomes 6
- **THEN** the second player has 1 available PP+1 use

#### Scenario: Reset without use
- **GIVEN** the second player has not used PP+1 before turn 6
- **WHEN** the second player's turn number becomes 6
- **THEN** the second player has 1 available PP+1 use and does not gain an extra use

#### Scenario: Total uses capped
- **GIVEN** the second player has already used PP+1 twice
- **WHEN** they attempt to activate PP+1 again
- **THEN** the activation is rejected and Current PP does not change

### Requirement: PP+1 activation timing
The system SHALL allow the second player to activate PP+1 only during their own turn.

#### Scenario: Activation outside own turn
- **GIVEN** it is not the second player's turn
- **WHEN** they attempt to activate PP+1
- **THEN** the activation is rejected

### Requirement: PP+1 increases current PP above max
Activating PP+1 SHALL increase the player's current PP by 1 and SHALL NOT change the maximum PP. The increase SHALL apply even when Current PP equals Max PP.

#### Scenario: Boost exceeds max
- **GIVEN** the second player has Current PP equal to Max PP
- **WHEN** they activate PP+1
- **THEN** Current PP becomes Max PP + 1 and Max PP is unchanged

#### Scenario: Boost with available PP capacity
- **GIVEN** the second player has Current PP less than Max PP
- **WHEN** they activate PP+1
- **THEN** Current PP increases by 1
