# CardInstance EffectiveAbilities Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose `CardInstance` current effective abilities as a DSL-readable property returning the full `Ability` list.

**Architecture:** Add a single computed property on `CardInstance` that reuses the existing `GetEffectiveAbilities()` logic and annotate it with `DslExposableAttribute` so runtime member access and metadata discovery both pick it up. Cover the change with one DSL compiler test for runtime access and one metadata test for editor/type metadata exposure.

**Tech Stack:** C#, .NET 8, xUnit, existing DSL compiler and metadata services

---

### Task 1: Add Regression Tests First

**Files:**
- Modify: `Server.Tests/DslCompilerTests.cs`
- Modify: `Server.Tests/DslMetadataServiceTests.cs`

- [ ] **Step 1: Write the failing DSL compiler test**

```csharp
[Fact]
public void CompileGetter_WithCardEffectiveAbilities_ReturnsAbilityObjects()
{
    var context = CreateTestGameContext();
    var player = context.GetRequestingPlayer();
    var card = new CardInstance(Guid.NewGuid(), 1, player.PlayerId);

    var ability = new Ability(
        new AbilityConfig { Id = 42, Name = "Test Ability", Description = "desc" },
        new Mock<IConditionFactory>().Object,
        new Mock<ITargetFactory>().Object,
        new Mock<TargetSelectionService>().Object,
        card);

    card.Abilities.Add(ability);

    var getter = _compiler.CompileGetter<CardInstance>("self.effectiveAbilities");

    var result = getter(card, context);

    var abilities = Assert.IsAssignableFrom<IReadOnlyList<Ability>>(result);
    var exposed = Assert.Single(abilities);
    Assert.Equal("Test Ability", exposed.Name);
}
```

- [ ] **Step 2: Run the compiler test to verify it fails**

Run: `dotnet test "Server.Tests/ShadowverseBeyond.Server.Tests.csproj" --filter "CompileGetter_WithCardEffectiveAbilities_ReturnsAbilityObjects"`
Expected: FAIL because `CardInstance` does not yet expose `EffectiveAbilities`.

- [ ] **Step 3: Write the failing metadata test**

```csharp
[Fact]
public void BuildMetadata_Exposes_CardInstance_EffectiveAbilities_Property()
{
    var registry = new FunctionRegistry(NullLogger<FunctionRegistry>.Instance);
    var service = new DslMetadataService(registry);

    var metadata = service.GetDslMetadata();

    Assert.True(metadata.TypeMetadata.TryGetValue("CardInstance", out var typeMetadata));
    var property = Assert.Single(typeMetadata!.Properties.Where(p => string.Equals(p.Name, "effectiveAbilities", StringComparison.OrdinalIgnoreCase)));
    Assert.Equal("IReadOnlyList<Ability>", property.TypeName);
}
```

- [ ] **Step 4: Run the metadata test to verify it fails**

Run: `dotnet test "Server.Tests/ShadowverseBeyond.Server.Tests.csproj" --filter "BuildMetadata_Exposes_CardInstance_EffectiveAbilities_Property"`
Expected: FAIL because metadata does not yet contain `effectiveAbilities`.

### Task 2: Implement Minimal Production Change

**Files:**
- Modify: `Server/Domain/Entities/CardInstance.cs`

- [ ] **Step 1: Add the computed DSL property**

```csharp
[DslExposable("当前有效能力", Description = "卡牌当前生效中的完整能力列表")]
public IReadOnlyList<Ability> EffectiveAbilities => GetEffectiveAbilities();
```

- [ ] **Step 2: Keep the existing private helper as the single source of truth**

```csharp
private IReadOnlyList<Ability> GetEffectiveAbilities()
{
    var context = GameServiceProvider.CurrentGameContext;
    if (context?.AbilityManager != null)
    {
        return context.AbilityManager
            .GetEffectiveAbilitiesForCard(InstanceId, context)
            .OfType<Ability>()
            .ToList();
    }

    return Abilities;
}
```

### Task 3: Verify the Change

**Files:**
- Test: `Server.Tests/DslCompilerTests.cs`
- Test: `Server.Tests/DslMetadataServiceTests.cs`

- [ ] **Step 1: Run the two focused tests**

Run: `dotnet test "Server.Tests/ShadowverseBeyond.Server.Tests.csproj" --filter "CompileGetter_WithCardEffectiveAbilities_ReturnsAbilityObjects|BuildMetadata_Exposes_CardInstance_EffectiveAbilities_Property"`
Expected: PASS

- [ ] **Step 2: Run a slightly wider safety check for DSL tests**

Run: `dotnet test "Server.Tests/ShadowverseBeyond.Server.Tests.csproj" --filter "FullyQualifiedName~DslCompilerTests|FullyQualifiedName~DslMetadataServiceTests"`
Expected: PASS

- [ ] **Step 3: Record completion without git operations**

Per repository `AGENTS.md`, do not perform `git commit` or branch operations unless the user explicitly asks.
