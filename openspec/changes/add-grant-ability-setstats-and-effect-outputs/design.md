## Context
Forestcraft conversion needs (1) dynamic ability grants and (2) chained abilities targeting cards created by the previous effect. We also need a set-stats effect without introducing a new event type.

## Goals / Non-Goals
- Goals:
  - Provide a GrantAbility effect backed by the existing Buff system.
  - Allow chained abilities to target cards created by the triggering effect via EffectCompletedEvent.
  - Provide a SetStats effect that uses StatChangedEvent with a set-layer modifier.
- Non-Goals:
  - Add random tie-breaking for .take(1).
  - Add new history tracking beyond existing graveyard queries.

## Decisions
- GrantAbility creates a composite Buff with AbilityBuffEffect and uses BuffService.ApplyBuffProcess.
- EffectCompletedEvent gains a GeneratedCards list sourced from effect-generated events (e.g., Summon, AddCardToHand, CopyCard).
- SetStats uses StatChangedEvent with a new modifier-layer parameter (default Additive); SetStats forces Set layer.

## GeneratedCards sourcing rules
- Collect cards from effect-generated events in this order: SummonEvent.SummonedCard, AddCardToHandEvent.CardsToAdd, CopyCardEvent.CopiedCard.
- Ignore nulls and deduplicate by InstanceId while preserving first-seen order.
- GeneratedCards is for chained targeting only and does not replace ResolvedTargets.

## Risks / Trade-offs
- GeneratedCards collection must be consistent across event types or chained targets will be incomplete.
- Updating StatChangedEvent signature is a behavior change; ensure existing callers default to Additive.
- GrantAbility must respect stacking rules (non-stackable flags) to avoid duplicate temporary abilities.

## Migration Plan
1. Implement GrantAbility and SetStats effects plus EffectCompletedEvent.GeneratedCards.
2. Update Forestcraft crest same-name summon to query graveyard crests by name.
3. Validate affected card scenarios.

## Open Questions
- Which generated events must contribute to GeneratedCards beyond Summon/AddCardToHand/CreateCard/CopyCard?
