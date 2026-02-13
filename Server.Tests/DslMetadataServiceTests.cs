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
    }
}
