using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Controllers;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class LocalizationControllerTests
    {
        [Fact]
        public void GetEnumLocalization_EventReasonSource_ReturnsExpectedEntries()
        {
            var controller = new LocalizationController(NullLogger<LocalizationController>.Instance);

            var result = controller.GetEnumLocalization("EventReasonSource");
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<Dictionary<string, string>>(ok.Value);

            Assert.Equal("系统", payload["System"]);
            Assert.Equal("能力", payload["Ability"]);
        }

        [Fact]
        public void GetEnumLocalization_DeckKeyword_ReturnsUnlimitedDeck()
        {
            var controller = new LocalizationController(NullLogger<LocalizationController>.Instance);

            var result = controller.GetEnumLocalization("DeckKeyword");
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<Dictionary<string, string>>(ok.Value);

            Assert.Equal("无垠界域牌组", payload["UnlimitedDeck"]);
        }
    }
}
