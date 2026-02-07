# Change: Separate Countdown Field From Health

## Why
Countdown uses Health today, which makes amulets/crests ambiguous and risks misuse. A dedicated Countdown field preserves intent and keeps health semantics clean.

## What Changes
- Add a dedicated Countdown field to card definitions and DTOs.
- Initialize and track countdown from the Countdown field rather than Health.
- Migrate existing card data to populate Countdown where needed.
- **BREAKING**: Countdown is no longer inferred from Health.

## Impact
- Affected specs: card-countdown
- Affected code: CardDefinition, CardInstance countdown initialization, card data JSON, editor/DTO display
