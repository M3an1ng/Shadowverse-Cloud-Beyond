## ADDED Requirements

### Requirement: Battle Card Preview Panel
The battle UI SHALL render a card preview panel in the top-left corner of the battle screen that uses the same markup and CSS classes as the card editor preview slate.

#### Scenario: Initial state
- **WHEN** the battle screen is shown and no card has been selected
- **THEN** the preview panel shows a placeholder state indicating no card is selected

### Requirement: Update Preview On Card Click
The system SHALL update the battle preview panel when a player clicks a card in the battle UI.

#### Scenario: Click hand card
- **WHEN** the player clicks a card element representing a hand card
- **THEN** the preview panel renders that card's preview slate with name, cost, description, class, type, rarity, and follower stats if applicable

#### Scenario: Click field card
- **WHEN** the player clicks a card element representing a field card
- **THEN** the preview panel renders that card's preview slate using current stats when available

### Requirement: Visual Consistency With Card Editor
The preview panel SHALL match the card editor's preview slate appearance by reusing the same CSS classes and layout structure.

#### Scenario: Visual parity
- **WHEN** a card is rendered in the battle preview panel
- **THEN** the DOM structure uses `card-preview-container` and `preview-slate` with the same child elements as the card editor preview

### Requirement: Preview Buff Source Summary
The preview panel SHALL render a buff summary list beneath the card description and above action buttons. The list SHALL include a stat summary line at the top showing total Attack/Health/Cost deltas from applicable buffs, and then one entry per unique buff `EffectKey` in ascending key order. Each entry SHALL display the buff source card name and source ability name.

#### Scenario: Unique effect keys render once
- **WHEN** the previewed card has multiple buffs sharing the same `EffectKey`
- **THEN** the preview panel shows a single summary line for that `EffectKey`

#### Scenario: Ordered by effect key
- **WHEN** the previewed card has buffs with different `EffectKey` values
- **THEN** the preview panel lists them in ascending `EffectKey` order

#### Scenario: Stat summary line
- **WHEN** the previewed card has buffs that modify Attack, Health, or Cost
- **THEN** the preview panel renders a stat summary line above the per-effect entries showing total deltas

#### Scenario: Missing source excluded
- **WHEN** a buff `EffectKey` lacks a resolvable source card name or source ability name
- **THEN** no per-effect entry is rendered for that `EffectKey`

#### Scenario: No buffs
- **WHEN** the previewed card has no stat deltas and no buffs with an `EffectKey`
- **THEN** the buff summary list is not rendered

### Requirement: Preview Counter Values
The preview panel SHALL render a counter summary list beneath the card description and above the buff summary list. Each entry SHALL display the counter name and its current numeric value, ordered by counter key in ascending order. The following system counter keys SHALL use localized labels:
- `spellboost` → `魔力增幅数`
- `earthsigil` → `土之印数`

#### Scenario: Counter values render
- **WHEN** the previewed card has counter entries
- **THEN** the preview panel lists each counter with its display name and current value

#### Scenario: Localized system counters
- **WHEN** the previewed card includes `spellboost` or `earthsigil` counters
- **THEN** the preview panel uses the localized labels for those counters

#### Scenario: No counters
- **WHEN** the previewed card has no counter entries
- **THEN** the counter summary list is not rendered

#### Scenario: Zero-value counters filtered
- **WHEN** a counter value is 0
- **THEN** the preview panel omits that counter from the list

### Requirement: Preview Detail Art Modal
The preview panel SHALL render a detail-art button in the header aligned to the top-right, above the stat icons. When clicked, it SHALL show the card's `ArtDetail` image in a centered modal and dismiss the modal on any click. If the card has no `ArtDetail`, the button SHALL be disabled.

#### Scenario: Detail button enabled
- **WHEN** the previewed card has `ArtDetail`
- **THEN** the preview header shows an enabled detail button

#### Scenario: Detail button disabled
- **WHEN** the previewed card has no `ArtDetail`
- **THEN** the preview header shows a disabled detail button

#### Scenario: Open and close modal
- **WHEN** the player clicks the preview detail button
- **THEN** a centered modal shows the `ArtDetail` image and closes on any click
