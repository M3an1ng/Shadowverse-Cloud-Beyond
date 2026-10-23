## ADDED Requirements

### Requirement: Dual-class deck declaration
In `DualClass` mode, a deck SHALL declare one primary class and one secondary class. The secondary class MUST be different from the primary class.

#### Scenario: Reject identical primary and secondary class
- **WHEN** a client submits a dual-class deck where `secondaryClass` equals `primaryClass`
- **THEN** the system rejects the deck with a validation error

### Requirement: Dual-class class quotas
In `DualClass` mode, the system SHALL enforce: total deck size MUST be 40 cards, primary-class cards MUST be at most 24, and secondary-class cards MUST be at most 16.

#### Scenario: Reject primary-class overflow
- **WHEN** a dual-class deck contains more than 24 cards from the primary class
- **THEN** the system rejects the deck with a quota validation error

#### Scenario: Reject secondary-class overflow
- **WHEN** a dual-class deck contains more than 16 cards from the secondary class
- **THEN** the system rejects the deck with a quota validation error

#### Scenario: Accept exact 24 plus 16 split
- **WHEN** a dual-class deck contains exactly 24 primary-class cards and 16 secondary-class cards
- **THEN** the system accepts the class quota portion of validation

### Requirement: Dual-class allowed classes
In `DualClass` mode, collectible cards in a deck SHALL be limited to primary class, secondary class, and neutral class.

#### Scenario: Reject off-class card
- **WHEN** a dual-class deck contains a card from a class that is neither primary, secondary, nor neutral
- **THEN** the system rejects the deck with a class legality validation error

### Requirement: Neutral card policy in dual-class mode
In `DualClass` mode, neutral cards SHALL count toward total deck size but SHALL NOT consume either primary-class or secondary-class quota.

#### Scenario: Neutral cards do not consume class quotas
- **WHEN** a dual-class deck includes neutral cards while primary and secondary quotas are each within limits
- **THEN** the system validates class quotas independently of neutral card count
