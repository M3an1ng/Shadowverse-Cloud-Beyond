## ADDED Requirements

### Requirement: Evolution Eligibility Is Derived
Card definitions SHALL NOT include can_evolve. Evolution eligibility SHALL be computed at runtime from card type and keywords (followers evolve by default; BanEvolve forbids).

#### Scenario: Load card data without can_evolve
- **WHEN** card definitions are loaded from JSON or database
- **THEN** missing can_evolve does not affect evolvability; follower cards are evolvable unless BanEvolve is present

#### Scenario: BanEvolve prevents evolution
- **WHEN** a follower has the BanEvolve keyword
- **THEN** the card is reported as not evolvable and evolve requests are rejected
