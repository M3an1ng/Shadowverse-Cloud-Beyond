# Change: Ability action handles for card operation buttons

## Why
Current card-level action buttons (Act/Fusion/Enhance/etc.) cannot identify which specific ability is executed when multiple abilities provide the same action. This blocks precise validation, targeting, logging, and future UX options.

## What Changes
- Introduce ActionId-based ability actions for card operation buttons (Act/Fusion/Enhance/etc.).
- Server generates and validates action handles that map to a concrete ability.
- Action events carry the triggering ability identity so only the selected ability executes.

## Impact
- Affected specs: ability-actions (new)
- Affected code: GameService (playability/menus), DTOs (action handle payloads), Act/Fusion/Enhance request+event pipeline, AbilityManager filtering.
