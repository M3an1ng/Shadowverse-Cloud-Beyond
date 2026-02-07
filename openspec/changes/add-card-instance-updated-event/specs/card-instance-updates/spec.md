## ADDED Requirements
### Requirement: Card instance update event broadcast
The system SHALL emit a CardInstanceUpdated event at the end of a transaction containing the complete CardInstanceDto for each affected card instance.

#### Scenario: Buff application updates preview data
- **WHEN** a transaction applies a buff that changes a card's preview metadata
- **THEN** the transaction SHALL broadcast CardInstanceUpdated with the updated CardInstanceDto for that card

### Requirement: Batched updates per transaction
The system SHALL batch updated card instances into a single CardInstanceUpdated event per transaction.

#### Scenario: Multiple cards updated in one transaction
- **WHEN** a transaction updates multiple card instances
- **THEN** the system SHALL send one CardInstanceUpdated event containing all updated CardInstanceDto entries
