using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Tests.Scenarios
{
    public static class NeutralCustomScenarios
    {
        private const int DetectiveMagnifierId = 1401401;
        private const int LiaWardFollowerId = 1401402;

        public static IEnumerable<ScenarioCase> Build()
        {
            yield return BuildDetectiveMagnifierRemoveWardScenario();
        }

        private static ScenarioCase BuildDetectiveMagnifierRemoveWardScenario()
        {
            return new ScenarioCase
            {
                Id = "neutral-1401401-custom-remove-ward",
                Name = "Neutral 1401401 Detective Magnifier removes Ward via Act",
                FocusCardId = DetectiveMagnifierId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5
                    },
                    Player2 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "magnifier",
                            CardId = DetectiveMagnifierId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Field
                        },
                        new()
                        {
                            Key = "wardTarget",
                            CardId = LiaWardFollowerId,
                            Owner = ScenarioPlayer.Player2,
                            Zone = CardZone.Field,
                            RegisterAbilities = false
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new ActAction(ScenarioPlayer.Player1, "magnifier")
                },
                Choices = new Dictionary<string, ScenarioChoice>
                {
                    ["magnifier.default"] = new ScenarioChoice
                    {
                        CardKeys = new List<string> { "wardTarget" }
                    }
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCardKeyword("wardTarget", CardKeyword.Ward, shouldHave: false),
                    new LogKeywordRemovedEvents("wardTarget", "wardTarget-keyword-removed"),
                    new LogCardBuffSummary("wardTarget", "wardTarget-after-act"),
                    new LogCardBuffSummary("magnifier", "magnifier-after-act")
                }
            };
        }
    }
}
