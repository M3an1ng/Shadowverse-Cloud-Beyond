## 1. Schema and Policy API

- [x] 1.1 Add `DeckCardCopyLimitPolicy` entity and `DbSet` in `ApplicationDbContext`.
- [x] 1.2 Configure EF mapping and unique index on `(Environment, Rule, CardDefinitionId)`.
- [x] 1.3 Create migration for policy table and indexes.
- [x] 1.4 Implement policy management API (query by format, batch upsert, delete-to-default).
- [x] 1.5 Add admin-only write authorization checks for policy updates.

## 2. Validation Integration

- [x] 2.1 Extend `DeckValidationService` to resolve effective per-card limit from policy with fallback `3`.
- [x] 2.2 Replace hardcoded copy-limit checks with dynamic checks, including `0` ban semantics.
- [x] 2.3 Return actionable validation errors containing card identity and effective limit.
- [x] 2.4 Ensure create/update/import/match preflight paths all use the same validation behavior.

## 3. Default Deck Compliance

- [x] 3.1 Update default deck generation to apply effective per-card copy limits.
- [x] 3.2 Exclude cards with limit `0` from generated default deck candidates.
- [x] 3.3 Fail bootstrap safely when legal pool capacity cannot reach required deck size.

## 4. Frontend Feedback Alignment

- [x] 4.1 Load and cache format-scoped copy-limit policy in deck builder.
- [x] 4.2 Update warning computation to evaluate violations against effective per-card limits.
- [x] 4.3 Ensure multi-violation warnings render as multi-line messages.
- [x] 4.4 Surface backend save error reasons without losing card-specific limit details.

## 5. Verification

- [x] 5.1 Add backend tests for fallback `3` and configured `0/1/2/3` limits.
- [x] 5.2 Add tests for path consistency across create/update/import/match preflight.
- [x] 5.3 Add tests for default deck generation under restrictive policy configurations.
- [x] 5.4 Run impacted test/build checks and record verification results in change notes.

## Verification Notes

- `dotnet test Server.Tests/ShadowverseBeyond.Server.Tests.csproj --filter "FullyQualifiedName~DeckCopyLimitPolicyIntegrationTests" -v minimal` -> pass (`8/8`).
- `dotnet build Server/ShadowverseBeyond.Server.csproj -v minimal` -> pass (`0 errors`, `0 warnings`).
