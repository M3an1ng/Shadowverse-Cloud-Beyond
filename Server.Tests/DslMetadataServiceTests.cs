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
    }
}
