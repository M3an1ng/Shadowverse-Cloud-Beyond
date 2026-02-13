# Change: Add Tribe Buff Effect

## Why
- Tribe (种类) 目前主要通过 `CardInstance.AddTribeMarker` 直接写入，未纳入 Buff 生命周期管理。
- Reanimate 的“死者”种类是一个典型的动态效果，应该与其他临时/动态效果统一到 Buff 管线。

## What Changes
- Add a new buff effect type `TribeBuffEffect` for adding tribe markers to card instances.
- Apply/remove tribe markers during `BuffAppliedEvent` / `BuffRemovedEvent` commit lifecycle.
- Add `ApplyTribeBuff` to `IBuffService`/`BuffService` for effect handlers.
- Update `ReanimateEffectHandler` to add reanimate tribe via tribe buff rather than direct marker mutation.
- Extend buff definition parsing/copy support for tribe effects (`addtribe`).

## Impact
- Affected specs: `tribe-buff`
- Affected code:
  - `Server/Domain/Buffs/*`
  - `Server/Domain/Events/BuffAppliedEvent.cs`
  - `Server/Domain/Events/BuffRemovedEvent.cs`
  - `Server/Domain/Entities/CardInstance.cs`
  - `Server/Domain/Abilities/Effects/Handlers/ReanimateEffectHandler.cs`
  - `Server.Tests/*`