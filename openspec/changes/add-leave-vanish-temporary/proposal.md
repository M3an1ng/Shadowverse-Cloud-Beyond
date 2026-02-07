# Change: Add temporary LeaveVanish support

## Why
Abysscraft conversion needs a LeaveVanish rule before event intercept effects are available.

## What Changes
- Add a LeaveVanish keyword and apply it during destruction/leave handling to convert to vanish (Removed).
- Ensure LeaveVanish does not increase graveyard counters or trigger graveyard-based effects.

## Impact
- Affected specs: leave-vanish
- Affected code: Card keywords, destroy/move events, conversion data, tests
