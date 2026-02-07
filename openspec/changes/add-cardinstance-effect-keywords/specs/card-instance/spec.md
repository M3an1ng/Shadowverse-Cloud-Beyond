## ADDED Requirements
### Requirement: CardInstance exposes effect keywords
The system SHALL expose a CardInstance.EffectKeywords property that returns a de-duplicated collection of EffectKeyword values aggregated from the card's abilities.

#### Scenario: Aggregate and de-duplicate keywords
- **WHEN** a card has multiple abilities that include overlapping EffectKeyword values
- **THEN** EffectKeywords contains each keyword once

#### Scenario: Ignore EffectKeyword.None
- **WHEN** any ability keyword is EffectKeyword.None
- **THEN** EffectKeywords excludes EffectKeyword.None

#### Scenario: Do not filter by silence or enablement
- **WHEN** a card is silenced or an ability is marked disabled
- **THEN** EffectKeywords still reflects all ability keywords (no filtering)

### Requirement: EffectKeywords is available in DSL metadata
The system SHALL expose EffectKeywords to the DSL metadata as a list of EffectKeyword values.

#### Scenario: DSL metadata includes EffectKeywords
- **WHEN** DSL metadata is generated
- **THEN** CardInstance lists EffectKeywords with the EffectKeyword enum type