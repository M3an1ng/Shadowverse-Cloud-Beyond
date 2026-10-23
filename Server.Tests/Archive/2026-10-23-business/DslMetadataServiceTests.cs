using System;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Services;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class DslMetadataServiceTests
    {
        [Fact]
        public void BuildMetadata_Hides_GameEventBase_Fields()
        {
            var registry = new FunctionRegistry(NullLogger<FunctionRegistry>.Instance);
            var service = new DslMetadataService(registry);

            var metadata = service.GetDslMetadata();
            Assert.NotNull(metadata);
            Assert.True(metadata.TypeMetadata.TryGetValue("GameEventBase", out var typeMetadata));
            Assert.NotNull(typeMetadata);

            var forbiddenFields = new[]
            {
                "eventType",
                "eventId",
                "timestamp",
                "source",
                "target"
            };

            Assert.DoesNotContain(typeMetadata.Properties, prop =>
                forbiddenFields.Any(field => string.Equals(prop.Name, field, StringComparison.OrdinalIgnoreCase)));
        }

        [Fact]
        public void BuildMetadata_Exposes_ByType_Function_With_EventType_Parameter()
        {
            var registry = new FunctionRegistry(NullLogger<FunctionRegistry>.Instance);
            var service = new DslMetadataService(registry);

            var metadata = service.GetDslMetadata();
            Assert.NotNull(metadata);

            var byType = metadata.Functions.FirstOrDefault(f =>
                string.Equals(f.Name, "byType", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(byType);
            Assert.Equal("IEnumerable<IGameEvent>", byType!.ReturnTypeName);
            Assert.Equal(2, byType.Parameters.Count);
            Assert.Equal("EventType", byType.Parameters[1].TypeName);
        }

        [Fact]
        public void BuildMetadata_Always_Contains_EventType_Enum()
        {
            var registry = new FunctionRegistry(NullLogger<FunctionRegistry>.Instance);
            var service = new DslMetadataService(registry);

            var metadata = service.GetDslMetadata();
            Assert.NotNull(metadata);

            var eventTypeEnum = metadata.EnumTypes.FirstOrDefault(e =>
                string.Equals(e.Name, "EventType", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(eventTypeEnum);
            Assert.Contains("Damage", eventTypeEnum!.Values);
            Assert.Contains("Destroy", eventTypeEnum.Values);
        }

        [Fact]
        public void BuildMetadata_Contains_EventReasonSource_Enum()
        {
            var registry = new FunctionRegistry(NullLogger<FunctionRegistry>.Instance);
            var service = new DslMetadataService(registry);

            var metadata = service.GetDslMetadata();
            Assert.NotNull(metadata);

            var reasonSourceEnum = metadata.EnumTypes.FirstOrDefault(e =>
                string.Equals(e.Name, "EventReasonSource", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(reasonSourceEnum);
            Assert.Contains("System", reasonSourceEnum!.Values);
            Assert.Contains("Ability", reasonSourceEnum.Values);
        }

        [Fact]
        public void BuildMetadata_Contains_Frequency_Function()
        {
            var registry = new FunctionRegistry(NullLogger<FunctionRegistry>.Instance);
            var service = new DslMetadataService(registry);

            var metadata = service.GetDslMetadata();
            Assert.NotNull(metadata);

            var frequency = metadata.Functions.FirstOrDefault(f =>
                string.Equals(f.Name, "frequency", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(frequency);
            Assert.Equal("Int32", frequency!.ReturnTypeName);
            Assert.Equal(2, frequency.Parameters.Count);
        }

        [Fact]
        public void BuildMetadata_Exposes_Db_Context_With_EnvironmentScoped_CardPools()
        {
            var registry = new FunctionRegistry(NullLogger<FunctionRegistry>.Instance);
            var service = new DslMetadataService(registry);

            var metadata = service.GetDslMetadata();
            Assert.NotNull(metadata);

            var dbContext = metadata.ContextObjects.FirstOrDefault(obj =>
                string.Equals(obj.Name, "db", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(dbContext);
            Assert.Equal("CardDefinitionDatabaseRoot", dbContext!.TypeName);
            Assert.Contains(dbContext.Properties, prop => string.Equals(prop.Name, "rotationCards", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(dbContext.Properties, prop => string.Equals(prop.Name, "unlimitedCards", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(dbContext.Properties, prop => string.Equals(prop.Name, "openCards", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(dbContext.Properties, prop => string.Equals(prop.Name, "currentEnvironmentCards", StringComparison.OrdinalIgnoreCase));

            Assert.True(metadata.TypeMetadata.TryGetValue("CardDefinitionDatabaseRoot", out var typeMetadata));
            Assert.NotNull(typeMetadata);
            Assert.Contains(typeMetadata!.Properties, prop => string.Equals(prop.Name, "rotationCards", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(typeMetadata.Properties, prop => string.Equals(prop.Name, "unlimitedCards", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(typeMetadata.Properties, prop => string.Equals(prop.Name, "openCards", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(typeMetadata.Properties, prop => string.Equals(prop.Name, "currentEnvironmentCards", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void BuildMetadata_Exposes_CurrentEnvironment_On_GameContext()
        {
            var registry = new FunctionRegistry(NullLogger<FunctionRegistry>.Instance);
            var service = new DslMetadataService(registry);

            var metadata = service.GetDslMetadata();
            Assert.NotNull(metadata);

            var context = metadata.ContextObjects.FirstOrDefault(obj =>
                string.Equals(obj.Name, "context", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(context);
            Assert.Contains(context!.Properties, prop =>
                string.Equals(prop.Name, "currentEnvironment", StringComparison.OrdinalIgnoreCase)
                && string.Equals(prop.TypeName, "DeckEnvironment", StringComparison.OrdinalIgnoreCase));

            Assert.True(metadata.TypeMetadata.TryGetValue("GameContext", out var gameContextType));
            Assert.NotNull(gameContextType);
            Assert.Contains(gameContextType!.Properties, prop =>
                string.Equals(prop.Name, "currentEnvironment", StringComparison.OrdinalIgnoreCase)
                && string.Equals(prop.TypeName, "DeckEnvironment", StringComparison.OrdinalIgnoreCase));

            var deckEnvironmentEnum = metadata.EnumTypes.FirstOrDefault(enumType =>
                string.Equals(enumType.Name, "DeckEnvironment", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(deckEnvironmentEnum);
            Assert.Contains("Rotation", deckEnvironmentEnum!.Values);
        }

        [Fact]
        public void BuildMetadata_Exposes_CardInstance_EffectiveAbilities_Property()
        {
            var registry = new FunctionRegistry(NullLogger<FunctionRegistry>.Instance);
            var service = new DslMetadataService(registry);

            var metadata = service.GetDslMetadata();
            Assert.NotNull(metadata);
            Assert.True(metadata.TypeMetadata.TryGetValue("CardInstance", out var typeMetadata));

            var property = Assert.Single(typeMetadata!.Properties.Where(prop =>
                string.Equals(prop.Name, "effectiveAbilities", StringComparison.OrdinalIgnoreCase)));

            Assert.Equal("IReadOnlyList<Ability>", property.TypeName);
        }

        [Fact]
        public void BuildMetadata_Merges_EventParameterCache_Into_IGameEvent_TypeMetadata()
        {
            var registry = new FunctionRegistry(NullLogger<FunctionRegistry>.Instance);
            var service = new DslMetadataService(registry);

            var metadata = service.GetDslMetadata();
            Assert.NotNull(metadata);
            Assert.True(metadata.TypeMetadata.TryGetValue("IGameEvent", out var typeMetadata));

            Assert.Contains(typeMetadata!.Properties, prop =>
                string.Equals(prop.Name, "currentPlayerId", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(typeMetadata.Properties, prop =>
                string.Equals(prop.Name, "currentPlayerTurnNumber", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(typeMetadata.Properties, prop =>
                string.Equals(prop.Name, "sourceAbilityKeywords", StringComparison.OrdinalIgnoreCase));
        }
    }
}
