# Proposal: add-devtools-match-lab

## Summary
Add a Devtools Match Lab that starts AI or local two-player matches with configurable starting parameters for fast card testing.

## Motivation
Current testing requires data edits or scripts to simulate special match states. A devtools entry reduces friction and avoids impacting normal gameplay flows.

## Scope
- Add a devtools tool-card entry that navigates to Match Lab
- Match Lab supports AI and local two-player modes
- Configure starting parameters (starting PP, max PP, starting HP, starting hand size, seed)
- Provide a devtools-only API to start matches with the config
- Apply DevMatchConfig during match initialization (devtools flow only)

## Out of Scope
- Changes to normal play mode UI or rules
- Changes to AI behavior logic
- Card script editor

## Risks & Mitigations
- Risk: devtools config affects production matches
  - Mitigation: API is devtools-only with server-side auth checks
- Risk: invalid params cause broken game states
  - Mitigation: validate on server and enforce safe bounds

## Success Criteria
- Devtools access required to use Match Lab
- Match Lab can start AI/local matches successfully
- Config overrides apply only to devtools matches

## Open Questions
- How to check devtools permission (role/claim vs config flag)?
- Where to place the tool-card on the devtools home layout?

