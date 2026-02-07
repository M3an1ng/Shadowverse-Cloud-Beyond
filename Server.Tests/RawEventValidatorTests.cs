using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Infrastructure.Services;
using System;
using System.Text.Json.Nodes;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class RawEventValidatorTests
    {
        [Fact]
        public void Validate_missing_event_type_fails()
        {
            var validator = new RawEventValidator();
            var raw = new RawEventEnvelopeDto(
                2,
                "",
                "EV-000001",
                DateTime.UtcNow,
                null,
                Array.Empty<string>(),
                new JsonObject());

            Assert.False(validator.TryValidate(raw, out _));
        }

        [Fact]
        public void Validate_invalid_event_id_fails()
        {
            var validator = new RawEventValidator();
            var raw = new RawEventEnvelopeDto(
                2,
                "Damage",
                "not-an-id",
                DateTime.UtcNow,
                null,
                Array.Empty<string>(),
                new JsonObject());

            Assert.False(validator.TryValidate(raw, out _));
        }

        [Fact]
        public void Validate_ok_passes()
        {
            var validator = new RawEventValidator();
            var raw = new RawEventEnvelopeDto(
                2,
                "Damage",
                "EV-000001",
                DateTime.UtcNow,
                "C-000001",
                new[] { "C-000002" },
                new JsonObject { ["Damage"] = 1 });

            Assert.True(validator.TryValidate(raw, out _));
        }
    }
}
