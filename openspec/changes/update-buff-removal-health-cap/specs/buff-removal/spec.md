## ADDED Requirements
### Requirement: Cap Current Health After Health Buff Removal
The system SHALL clamp a card's current health to the lower of its pre-removal current health and its new max health when a removed buff modifies max health.

#### Scenario: Current health is preserved when below new max
- **WHEN** a health-modifying buff is removed and the card's current health is less than the new max health
- **THEN** the card's current health remains unchanged

#### Scenario: Current health is capped to new max
- **WHEN** a health-modifying buff is removed and the card's current health exceeds the new max health
- **THEN** the card's current health is reduced to the new max health