## Context
Evolution no longer swaps to a separate definition; it applies a fixed +2/+2 and immediate attack. The data model and editor still expose evolve_id and the runtime still carries EvolveDefinitionId, which is now unused. We also need to complete neutral card data conversion, including Act abilities for Detective's Magnifying Glass.

## Goals / Non-Goals
- Goals:
  - Remove evolve_id from data, schema, and runtime usage.
  - Ensure evolution logic stays fixed (+2/+2 and immediate attack) without definition swap.
  - Normalize Detective's Magnifying Glass to Act-based activation.
- Non-Goals:
  - Reintroduce evolve form card definitions.
  - Redesign the entire card editor UI beyond removing evolve_id.

## Decisions
- Decision: Drop evolve_id from card definitions and DB schema; evolution uses fixed stat change only.
- Decision: Represent Detective's Magnifying Glass activation as Act (EventType.Act) with Act preconditions, not CountdownFinished.

## Risks / Trade-offs
- Removing evolve_id is breaking for existing JSON data and editor flows; requires coordinated data migration and UI updates.
- Tests and AI logic referencing EvolveId must be updated to avoid null/removed fields.

## Migration Plan
1. Update DTOs/entities/mappings and remove evolve_id from JSON/card data.
2. Update schema and add a migration to drop the column.
3. Update editor and AI logic to use CanEvolve/keyword flags only.
4. Adjust tests and docs.

## Open Questions
- Should Act cost for Detective's Magnifying Glass be defined as 0 or configured per card?
- Is CountdownFinished still needed for any amulet in the neutral set after conversion?
