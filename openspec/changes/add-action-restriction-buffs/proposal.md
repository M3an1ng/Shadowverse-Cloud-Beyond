# Change: Add Action Restriction Buffs for Permission Checks

## Why
Keyword-only bans cannot express conditional restrictions such as "cannot attack if graveyard < 10".
We need a parameterized, rule-driven restriction model that can be evaluated consistently in playability
checks and action request validation.

## What Changes
- Add a buff/status-driven action restriction model with parameters (action type, optional play mode,
  condition DSL, reason, duration).
- Add a centralized permission evaluation service used by playability checks and action request validation.
- Expose restriction denial reasons to clients for UI messaging.

## Impact
- Affected specs: action-permissions
- Affected code: permission service, buff restriction model, playability checks, action request validation
