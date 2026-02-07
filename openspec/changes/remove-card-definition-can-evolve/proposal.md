# Change: Remove CardDefinition can_evolve flag

## Why
The evolution rule is now fixed (+2/+2 and immediate attack), and all followers should be evolvable by default. The can_evolve flag adds redundant data maintenance and conflicts with keyword-driven restrictions.

## What Changes
- Remove can_evolve from card definition schema and APIs (JSON, DTOs, database).
- Compute evolution eligibility from card type and keywords (followers evolve by default; BanEvolve forbids).
- Update editor, seed data, and tests to align. **BREAKING**

## Impact
- affected specs: card-data
- affected code: CardDefinition entity/DTOs, migrations, CardInstance.CanEvolve, AI evolve selection, card editor
