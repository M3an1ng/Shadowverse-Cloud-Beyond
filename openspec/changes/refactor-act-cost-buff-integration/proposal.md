# Change: Refactor Act cost computation with buff integration

## Why
Act cost is currently computed inside ActRequestedEvent and ignores buff-driven adjustments. We need a card-level Act cost entry that can apply aura/buff keyword modifiers while keeping request validation consistent.

## What Changes
- Move Act cost computation into CardInstance (card-level derived value).
- Add BuffConfig/IBuffEffect support for ability keyword value modifiers (e.g., Act).
- ActRequestedEvent uses the card-level Act cost.

## Impact
- Affected specs: act-cost (new), buffs (keyword modifiers)
- Affected code: Server/Domain/Entities/CardInstance.cs, Server/Domain/Events/ActionRequests/ActRequestedEvent.cs,
  Server/Domain/Buffs/IBuffEffect.cs, Server/Domain/Buffs/BuffConfig.cs, Server/Domain/Buffs/BuffFactory.cs,
  Server/Domain/Buffs/BuffService.cs, Server/Domain/Buffs/IBuffService.cs, Server/Domain/Abilities/Auras/AuraManager.cs
