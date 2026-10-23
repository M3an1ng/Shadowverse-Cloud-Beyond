using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Tests.Scenarios
{
    public static class ForestcraftCustomScenarios
    {
        private const int FairyId = 1010402;
        private const int CrestEikoId = 1510201;
        private const int ForbiddenExplorationId = 1511201;
        private const int NatureDissolverId = 1511301;
        private const int SproutOfHopeId = 1511302;
        private const int MysticGardenFairyId = 1511404;
        private const int NightmareFairyId = 1511405;
        private const int FairyUnityId = 1511406;
        private const int CrestFloraId = 1510103;
        private const int CrestSpringId = 1510105;
        private const int TeaPartyId = 1510107;
        private const int CrestWinterId = 1510203;
        private const int CrestSummerId = 1510205;
        private const int CrestAutumnId = 1510302;
        private const int FloraId = 1511101;
        private const int RozealId = 1511102;
        private const int SpringId = 1511103;
        private const int EikoId = 1511202;
        private const int SakuraId = 1511203;
        private const int SummerId = 1511204;
        private const int ForestFriendsId = 1511205;
        private const int NomadLandId = 1511303;
        private const int CommandingMiracleId = 1511304;
        private const int WinterId = 1511306;
        private const int FruitDragonId = 1511401;
        private const int AutumnId = 1511403;
        private const int WhiteDreamFairyId = 1511407;
        private const int NeutralCost1FollowerId = 1010402;
        private const int NeutralCost2FollowerId = 1511204;
        private const int NeutralCost3FollowerId = 1511403;
        private const int NeutralFollowerId = 1511407;

        public static IEnumerable<ScenarioCase> Build()
        {
            yield return BuildCrestEikoGrantScenario();
            yield return BuildForbiddenExplorationCostScenario();
            yield return BuildMysticGardenFairyChooseScenario();
            yield return BuildSproutOfHopeLastwordScenario();
            yield return BuildNightmareFairyStrikeScenario();
            yield return BuildFairyUnityCostScenario();
            yield return BuildNatureDissolverEnemyTurnEndScenario();
            yield return BuildCrestFloraDebuffScenario();
            yield return BuildCrestSpringTurnEndScenario();
            yield return BuildTeaPartyRecallScenario();
            yield return BuildCrestWinterDestroyScenario();
            yield return BuildCrestSummerDamageScenario();
            yield return BuildCrestAutumnDrawScenario();
            yield return BuildFloraFanfareScenario();
            yield return BuildRozealFanfareScenario();
            yield return BuildSpringFanfareScenario();
            yield return BuildEikoClashCrestScenario();
            yield return BuildCrestEikoLastwordStormScenario();
            yield return BuildSakuraSuperEvolveScenario();
            yield return BuildSummerFanfareScenario();
            yield return BuildForestFriendsDrawScenario();
            yield return BuildNomadLandActScenario();
            yield return BuildCommandingMiracleBuffScenario();
            yield return BuildWinterEvolveScenario();
            yield return BuildFruitDragonBuffScenario();
            yield return BuildAutumnFanfareScenario();
            yield return BuildWhiteDreamFairyEvolveScenario();
        }

        private static ScenarioCase BuildCrestEikoGrantScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1510201-custom-grant",
                Name = "Forestcraft 1510201 纹章:艾蔻 回合结束授予连击自毁",
                FocusCardId = CrestEikoId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = CrestEikoId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Emblem
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new TurnEndAction(ScenarioPlayer.Player1)
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCardAbilityEffect("focus", EventType.PlayCard, EffectType.Destroy, minimumCount: 1)
                }
            };
        }

        private static ScenarioCase BuildForbiddenExplorationCostScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511201-custom-cost0",
                Name = "Forestcraft 1511201 禁忌探索 谢幕检索费用归零",
                FocusCardId = ForbiddenExplorationId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = ForbiddenExplorationId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Field
                        },
                        new()
                        {
                            Key = "deckCard",
                            CardId = FairyId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Deck,
                            RegisterAbilities = false
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new DestroyAction(ScenarioPlayer.Player1, "focus")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Hand, FairyId, 1),
                    new ExpectPlayerZoneCardIdCost(ScenarioPlayer.Player1, CardZone.Hand, FairyId, 0, requireAll: true)
                }
            };
        }

        private static ScenarioCase BuildMysticGardenFairyChooseScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511404-custom-choose",
                Name = "Forestcraft 1511404 秘园妖精 抉择加入妖精并减费",
                FocusCardId = MysticGardenFairyId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 2,
                        CurrentPlayPoints = 2
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = MysticGardenFairyId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "focus")
                },
                Choices = new Dictionary<string, ScenarioChoice>
                {
                    ["choose_0"] = new ScenarioChoice
                    {
                        Indices = new List<int> { 1 }
                    }
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCardZone("focus", CardZone.Field),
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Hand, FairyId, 1),
                    new ExpectPlayerZoneCardIdCost(ScenarioPlayer.Player1, CardZone.Hand, FairyId, 0, requireAll: true)
                }
            };
        }

        private static ScenarioCase BuildSproutOfHopeLastwordScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511302-custom-lastword",
                Name = "Forestcraft 1511302 希望的萌芽 妖精谢幕能力",
                FocusCardId = SproutOfHopeId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 3,
                        CurrentPlayPoints = 3
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = SproutOfHopeId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "focus")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCardZone("focus", CardZone.Graveyard),
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Field, FairyId, 2),
                    new ExpectZoneCardAbilityEffect(ScenarioPlayer.Player1, CardZone.Field, FairyId, EffectType.Heal, EventType.Destroy, minimumCount: 1),
                    new ExpectZoneCardAbilityEffect(ScenarioPlayer.Player1, CardZone.Field, FairyId, EffectType.Draw, EventType.Destroy, minimumCount: 1)
                }
            };
        }

        private static ScenarioCase BuildNightmareFairyStrikeScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511405-custom-strike",
                Name = "Forestcraft 1511405 魇梦妖精 妖精获得打击伤害",
                FocusCardId = NightmareFairyId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 6,
                        CurrentPlayPoints = 6
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = NightmareFairyId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "focus")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCardZone("focus", CardZone.Field),
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Hand, FairyId, 3),
                    new ExpectZoneCardAbilityEffect(ScenarioPlayer.Player1, CardZone.Hand, FairyId, EffectType.Damage, EventType.Attack, minimumCount: 3, requireAll: true)
                }
            };
        }

        private static ScenarioCase BuildFairyUnityCostScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511406-custom-cost0",
                Name = "Forestcraft 1511406 妖精的团结 检索减费",
                FocusCardId = FairyUnityId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 3,
                        CurrentPlayPoints = 3
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = FairyUnityId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        },
                        new()
                        {
                            Key = "deckCard1",
                            CardId = FairyId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Deck,
                            RegisterAbilities = false
                        },
                        new()
                        {
                            Key = "deckCard2",
                            CardId = FairyId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Deck,
                            RegisterAbilities = false
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "focus")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCardZone("focus", CardZone.Graveyard),
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Hand, FairyId, 2),
                    new ExpectPlayerZoneCardIdCost(ScenarioPlayer.Player1, CardZone.Hand, FairyId, 0, requireAll: true)
                }
            };
        }

        private static ScenarioCase BuildNatureDissolverEnemyTurnEndScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511301-custom-clash",
                Name = "Forestcraft 1511301 自然默化者 交战赋予敌方回合结束",
                FocusCardId = NatureDissolverId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 7,
                        CurrentPlayPoints = 7,
                        LeaderHealth = 14
                    },
                    Player2 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = NatureDissolverId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        },
                        new()
                        {
                            Key = "enemy",
                            CardId = NatureDissolverId,
                            Owner = ScenarioPlayer.Player2,
                            Zone = CardZone.Field,
                            RegisterAbilities = false
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "focus"),
                    new AttackAction(ScenarioPlayer.Player1, "focus", "enemy"),
                    new TurnEndAction(ScenarioPlayer.Player1)
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCardZone("enemy", CardZone.Graveyard),
                    new ExpectPlayerHealth(ScenarioPlayer.Player1, 20)
                }
            };
        }

        private static ScenarioCase BuildCrestFloraDebuffScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1510103-custom-debuff",
                Name = "Forestcraft 1510103 纹章:季神·芙罗拉 入场减益",
                FocusCardId = CrestFloraId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 1,
                        CurrentPlayPoints = 1
                    },
                    Player2 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "crest",
                            CardId = CrestFloraId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Emblem
                        },
                        new()
                        {
                            Key = "ally1",
                            CardId = FairyId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Field,
                            RegisterAbilities = false
                        },
                        new()
                        {
                            Key = "ally2",
                            CardId = FairyId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Field,
                            RegisterAbilities = false
                        },
                        new()
                        {
                            Key = "ally3",
                            CardId = FairyId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Field,
                            RegisterAbilities = false
                        },
                        new()
                        {
                            Key = "play",
                            CardId = FairyId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        },
                        new()
                        {
                            Key = "enemy",
                            CardId = NeutralFollowerId,
                            Owner = ScenarioPlayer.Player2,
                            Zone = CardZone.Field,
                            RegisterAbilities = false
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "play")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCardStats("enemy", attack: 1, maxHealth: 2)
                }
            };
        }

        private static ScenarioCase BuildCrestSpringTurnEndScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1510105-custom-turnend",
                Name = "Forestcraft 1510105 纹章:“悠然可爱”·春 回合结束加入妖精并延长",
                FocusCardId = CrestSpringId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        PlayedCardCountThisTurn = 4
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "crest",
                            CardId = CrestSpringId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Emblem
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new TurnEndAction(ScenarioPlayer.Player1)
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Hand, FairyId, 1),
                    new ExpectCardCountdown("crest", 2)
                }
            };
        }

        private static ScenarioCase BuildTeaPartyRecallScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1510107-custom-teaparty",
                Name = "Forestcraft 1510107 四季的茶话会 回收纹章与PP",
                FocusCardId = TeaPartyId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 4,
                        CurrentPlayPoints = 4
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = TeaPartyId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        },
                        new()
                        {
                            Key = "crestField",
                            CardId = CrestSpringId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Emblem,
                            RegisterAbilities = false
                        },
                        new()
                        {
                            Key = "crestGrave",
                            CardId = CrestFloraId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Graveyard,
                            RegisterAbilities = false
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "focus")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Emblem, CrestFloraId, 1),
                    new ExpectEventTypeOccurred(EventType.PpUpdated)
                }
            };
        }

        private static ScenarioCase BuildCrestWinterDestroyScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1510203-custom-turnend",
                Name = "Forestcraft 1510203 纹章:“傲雪淩霜”·冬 回合结束破坏",
                FocusCardId = CrestWinterId,
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
                            Key = "crest",
                            CardId = CrestWinterId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Emblem
                        },
                        new()
                        {
                            Key = "enemy",
                            CardId = NeutralFollowerId,
                            Owner = ScenarioPlayer.Player2,
                            Zone = CardZone.Field,
                            RegisterAbilities = false
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new TurnEndAction(ScenarioPlayer.Player1)
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCardZone("enemy", CardZone.Graveyard)
                }
            };
        }

        private static ScenarioCase BuildCrestSummerDamageScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1510205-custom-damage",
                Name = "Forestcraft 1510205 纹章:“炽烈激情”·夏 回合结束伤害",
                FocusCardId = CrestSummerId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        PlayedCardCountThisTurn = 4
                    },
                    Player2 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        LeaderHealth = 20
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "crest",
                            CardId = CrestSummerId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Emblem
                        },
                        new()
                        {
                            Key = "enemy",
                            CardId = NeutralFollowerId,
                            Owner = ScenarioPlayer.Player2,
                            Zone = CardZone.Field,
                            RegisterAbilities = false
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new TurnEndAction(ScenarioPlayer.Player1)
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCardStats("enemy", currentHealth: 2),
                    new ExpectPlayerHealth(ScenarioPlayer.Player2, 19),
                    new ExpectCardCountdown("crest", 2)
                }
            };
        }

        private static ScenarioCase BuildCrestAutumnDrawScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1510302-custom-draw",
                Name = "Forestcraft 1510302 纹章:“馥郁柔和”·秋 回合结束抽牌",
                FocusCardId = CrestAutumnId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        PlayedCardCountThisTurn = 4
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "crest",
                            CardId = CrestAutumnId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Emblem
                        },
                        new()
                        {
                            Key = "deckCard",
                            CardId = NeutralFollowerId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Deck,
                            RegisterAbilities = false
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new TurnEndAction(ScenarioPlayer.Player1)
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Hand, NeutralFollowerId, 1),
                    new ExpectCardCountdown("crest", 2)
                }
            };
        }

        private static ScenarioCase BuildFloraFanfareScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511101-custom-fanfare",
                Name = "Forestcraft 1511101 季神·芙罗拉 战吼纹章与妖精",
                FocusCardId = FloraId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 4,
                        CurrentPlayPoints = 4
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = FloraId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "focus")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCardZone("focus", CardZone.Field),
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Emblem, CrestFloraId, 1),
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Field, FairyId, 2)
                }
            };
        }

        private static ScenarioCase BuildRozealFanfareScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511102-custom-fanfare",
                Name = "Forestcraft 1511102 御风领主·萝洁爱尔 战吼检索",
                FocusCardId = RozealId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 6,
                        CurrentPlayPoints = 6
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = RozealId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        },
                        new()
                        {
                            Key = "deck3",
                            CardId = NeutralCost3FollowerId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Deck,
                            RegisterAbilities = false
                        },
                        new()
                        {
                            Key = "deck2",
                            CardId = NeutralCost2FollowerId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Deck,
                            RegisterAbilities = false
                        },
                        new()
                        {
                            Key = "deck1",
                            CardId = NeutralCost1FollowerId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Deck,
                            RegisterAbilities = false
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "focus")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCardZone("focus", CardZone.Field),
                    new ExpectCardZone("deck3", CardZone.Field),
                    new ExpectCardZone("deck2", CardZone.Field),
                    new ExpectCardZone("deck1", CardZone.Field),
                    new ExpectCardKeyword("deck3", CardKeyword.Ward),
                    new ExpectCardKeyword("deck2", CardKeyword.Ward),
                    new ExpectCardKeyword("deck1", CardKeyword.Ward),
                    new ExpectCardAbilityEffect("deck3", EventType.Clash, EffectType.Return, minimumCount: 1),
                    new ExpectCardAbilityEffect("deck2", EventType.Clash, EffectType.Return, minimumCount: 1),
                    new ExpectCardAbilityEffect("deck1", EventType.Clash, EffectType.Return, minimumCount: 1)
                }
            };
        }

        private static ScenarioCase BuildSpringFanfareScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511103-custom-fanfare",
                Name = "Forestcraft 1511103 “悠然可爱”·春 战吼纹章与治疗",
                FocusCardId = SpringId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 1,
                        CurrentPlayPoints = 1,
                        PlayedCardCountThisTurn = 4,
                        LeaderHealth = 10
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = SpringId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "focus")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectPlayerHealth(ScenarioPlayer.Player1, 13),
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Emblem, CrestSpringId, 1)
                }
            };
        }

        private static ScenarioCase BuildEikoClashCrestScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511202-custom-clash",
                Name = "Forestcraft 1511202 艾蔻 交战追加纹章",
                FocusCardId = EikoId,
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
                            Key = "focus",
                            CardId = EikoId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Field
                        },
                        new()
                        {
                            Key = "enemy",
                            CardId = NeutralFollowerId,
                            Owner = ScenarioPlayer.Player2,
                            Zone = CardZone.Field,
                            RegisterAbilities = false
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new AttackAction(ScenarioPlayer.Player1, "focus", "enemy")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Emblem, CrestEikoId, 1)
                }
            };
        }

        private static ScenarioCase BuildCrestEikoLastwordStormScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1510201-custom-lastword",
                Name = "Forestcraft 1510201 纹章:艾蔻 谢幕召唤疾驰",
                FocusCardId = CrestEikoId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "crest",
                            CardId = CrestEikoId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Emblem
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new DestroyAction(ScenarioPlayer.Player1, "crest")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Field, EikoId, 1),
                    new ExpectZoneCardKeyword(ScenarioPlayer.Player1, CardZone.Field, EikoId, CardKeyword.Storm, minimumCount: 1)
                }
            };
        }

        private static ScenarioCase BuildSakuraSuperEvolveScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511203-custom-superevolve",
                Name = "Forestcraft 1511203 狐樱花 超进化强化",
                FocusCardId = SakuraId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 7,
                        MaxPlayPoints = 1,
                        CurrentPlayPoints = 1,
                        MaxSuperEvolutionPoints = 1,
                        CurrentSuperEvolutionPoints = 1
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = SakuraId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "focus"),
                    new SuperEvolveAction(ScenarioPlayer.Player1, "focus")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCardStats("focus", attack: 5, maxHealth: 5)
                }
            };
        }

        private static ScenarioCase BuildSummerFanfareScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511204-custom-fanfare",
                Name = "Forestcraft 1511204 “炽烈激情”·夏 战吼纹章",
                FocusCardId = SummerId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 2,
                        CurrentPlayPoints = 2
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = SummerId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "focus")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Emblem, CrestSummerId, 1),
                    new ExpectCardKeyword("focus", CardKeyword.Bane)
                }
            };
        }

        private static ScenarioCase BuildForestFriendsDrawScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511205-custom-draw",
                Name = "Forestcraft 1511205 森友合击 连击抽牌",
                FocusCardId = ForestFriendsId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 2,
                        CurrentPlayPoints = 2,
                        PlayedCardCountThisTurn = 3
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = ForestFriendsId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        },
                        new()
                        {
                            Key = "deckCard",
                            CardId = NeutralFollowerId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Deck,
                            RegisterAbilities = false
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "focus")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Hand, NeutralFollowerId, 1)
                }
            };
        }

        private static ScenarioCase BuildNomadLandActScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511303-custom-act",
                Name = "Forestcraft 1511303 游牧之地 启动强化",
                FocusCardId = NomadLandId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 1,
                        CurrentPlayPoints = 1,
                        PlayedCardCountThisTurn = 3
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = NomadLandId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Field
                        },
                        new()
                        {
                            Key = "ally1",
                            CardId = FairyId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Field,
                            RegisterAbilities = false
                        },
                        new()
                        {
                            Key = "ally2",
                            CardId = FairyId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Field,
                            RegisterAbilities = false
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new ActAction(ScenarioPlayer.Player1, "focus")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCardStats("ally1", attack: 2, maxHealth: 2),
                    new ExpectCardStats("ally2", attack: 2, maxHealth: 2)
                }
            };
        }

        private static ScenarioCase BuildCommandingMiracleBuffScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511304-custom-buff",
                Name = "Forestcraft 1511304 指挥的奇迹 抉择强化",
                FocusCardId = CommandingMiracleId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 2,
                        CurrentPlayPoints = 2
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = CommandingMiracleId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        },
                        new()
                        {
                            Key = "ally",
                            CardId = FairyId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Field,
                            RegisterAbilities = false
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "focus")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCardStats("ally", attack: 2, maxHealth: 3)
                }
            };
        }

        private static ScenarioCase BuildWinterEvolveScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511306-custom-evolve",
                Name = "Forestcraft 1511306 “傲雪淩霜”·冬 进化减益",
                FocusCardId = WinterId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 4,
                        CurrentPlayPoints = 4,
                        MaxEvolutionPoints = 1,
                        CurrentEvolutionPoints = 1
                    },
                    Player2 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = WinterId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        },
                        new()
                        {
                            Key = "enemy",
                            CardId = NeutralFollowerId,
                            Owner = ScenarioPlayer.Player2,
                            Zone = CardZone.Field,
                            RegisterAbilities = false
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "focus"),
                    new EvolveAction(ScenarioPlayer.Player1, "focus")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCardStats("enemy", attack: 1, maxHealth: 2),
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Emblem, CrestWinterId, 1)
                }
            };
        }

        private static ScenarioCase BuildFruitDragonBuffScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511401-custom-increase",
                Name = "Forestcraft 1511401 拾果龙 属性提升增益",
                FocusCardId = FruitDragonId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 2,
                        CurrentPlayPoints = 2
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "dragon",
                            CardId = FruitDragonId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Field
                        },
                        new()
                        {
                            Key = "ally",
                            CardId = FairyId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Field,
                            RegisterAbilities = false
                        },
                        new()
                        {
                            Key = "support",
                            CardId = CommandingMiracleId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "support")
                },
                Choices = new Dictionary<string, ScenarioChoice>
                {
                    ["指挥的奇迹-选择-强化.target1"] = new ScenarioChoice
                    {
                        CardKeys = new List<string> { "dragon" }
                    }
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectCardStats("ally", attack: 2, maxHealth: 2)
                }
            };
        }

        private static ScenarioCase BuildAutumnFanfareScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511403-custom-fanfare",
                Name = "Forestcraft 1511403 “馥郁柔和”·秋 战吼纹章",
                FocusCardId = AutumnId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 3,
                        CurrentPlayPoints = 3
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = AutumnId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "focus")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Emblem, CrestAutumnId, 1),
                    new ExpectCardKeyword("focus", CardKeyword.FreeEvolve)
                }
            };
        }

        private static ScenarioCase BuildWhiteDreamFairyEvolveScenario()
        {
            return new ScenarioCase
            {
                Id = "forestcraft-1511407-custom-evolve",
                Name = "Forestcraft 1511407 白梦妖精 进化召唤突进",
                FocusCardId = WhiteDreamFairyId,
                Setup = new ScenarioSetup
                {
                    Player1 = new ScenarioPlayerSetup
                    {
                        TurnNumber = 5,
                        MaxPlayPoints = 3,
                        CurrentPlayPoints = 3,
                        MaxEvolutionPoints = 1,
                        CurrentEvolutionPoints = 1
                    },
                    Cards = new List<ScenarioCardSpec>
                    {
                        new()
                        {
                            Key = "focus",
                            CardId = WhiteDreamFairyId,
                            Owner = ScenarioPlayer.Player1,
                            Zone = CardZone.Hand
                        }
                    }
                },
                Actions = new List<ScenarioAction>
                {
                    new PlayCardAction(ScenarioPlayer.Player1, "focus"),
                    new EvolveAction(ScenarioPlayer.Player1, "focus")
                },
                Expectations = new List<IScenarioExpectation>
                {
                    new ExpectPlayerZoneCardIdCount(ScenarioPlayer.Player1, CardZone.Field, WhiteDreamFairyId, 2),
                    new ExpectZoneCardKeyword(ScenarioPlayer.Player1, CardZone.Field, WhiteDreamFairyId, CardKeyword.Rush, minimumCount: 1)
                }
            };
        }
    }
}
