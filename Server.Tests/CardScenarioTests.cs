using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Tests.Scenarios;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class CardScenarioTests
    {
        [Theory]
        [MemberData(nameof(AllCraftCases))]
        public void RunScenario(ScenarioCase scenario)
        {
            ScenarioRunner.RunScenario(scenario);
        }

        public static IEnumerable<object[]> AllCraftCases()
        {
            foreach (var scenario in ForestcraftCustomScenarios.Build())
            {
                yield return new object[] { scenario };
            }

            foreach (var scenario in NeutralCustomScenarios.Build())
            {
                yield return new object[] { scenario };
            }

            var crafts = new[]
            {
                "neutral",
                "forestcraft",
                "swordcraft",
                "runecraft",
                "dragoncraft",
                "abysscraft",
                "havencraft",
                "portalcraft"
            };

            foreach (var craft in crafts)
            {
                if (!ScenarioCaseFactory.HasCraftData(craft))
                {
                    continue;
                }

                foreach (var scenario in ScenarioCaseFactory.BuildCraftCases(craft))
                {
                    yield return scenario;
                }
            }
        }
    }
}
