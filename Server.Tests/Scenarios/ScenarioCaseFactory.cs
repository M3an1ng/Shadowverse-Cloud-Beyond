using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Tests.Scenarios
{
    public static class ScenarioCaseFactory
    {
        private const int DefaultTurnNumber = 5;
        private const int DummyNeutralFollowerId = 1401404;
        private const int DummyNeutralAmuletId = 1401401;
        private const int DummyFairyId = 1010402;
        private const int DummySoldierId = 1020401;
        private const int DummyHandFollowerId = 1020401;

        private static readonly Dictionary<string, EventType[]> EffectEventMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Heal"] = new[] { EventType.Heal },
            ["Damage"] = new[] { EventType.Damage },
            ["Draw"] = new[] { EventType.Draw },
            ["Destroy"] = new[] { EventType.Destroy },
            ["Evolve"] = new[] { EventType.Evolve },
            ["Summon"] = new[] { EventType.Summon },
            ["GetToken"] = new[] { EventType.AddCardToHand },
            ["AddEarthSigil"] = new[] { EventType.Summon },
            ["CreateCard"] = new[] { EventType.CreateCard },
            ["CopyCard"] = new[] { EventType.CopyCard },
            ["Transform"] = new[] { EventType.Transform },
            ["Return"] = new[] { EventType.Return },
            ["Shuffle"] = new[] { EventType.Shuffle },
            ["Tuck"] = new[] { EventType.Tuck },
            ["Vanish"] = new[] { EventType.Vanish },
            ["AddDeck"] = new[] { EventType.AddDeck },
            ["SetCountdown"] = new[] { EventType.CountdownTick },
            ["SetAttackTimes"] = new[] { EventType.AttackTimesUpdated },
            ["ModifyPP"] = new[] { EventType.PpUpdated },
            ["ModifyPPMax"] = new[] { EventType.PpMaxUpdated },
            ["ModifyEP"] = new[] { EventType.EpUpdated },
            ["ModifyEPMax"] = new[] { EventType.EpMaxUpdated },
            ["ModifyStats"] = new[] { EventType.StatChanged },
            ["SetKeyword"] = new[] { EventType.KeywordApplied },
            ["AddEmblem"] = new[] { EventType.AddEmblem },
            ["EarthRite"] = new[] { EventType.Destroy },
            ["Necromancy"] = new[] { EventType.GraveyardUpdated },
            ["Reanimate"] = new[] { EventType.Summon },
            ["SpellBoost"] = new[] { EventType.SpellBoost },
            ["Discard"] = new[] { EventType.Discard },
            ["Reveal"] = new[] { EventType.CardRevealed }
        };

        public static bool HasCraftData(string craft)
        {
            return FindCraftDataPath(craft) != null;
        }

        public static IEnumerable<object[]> BuildCraftCases(string craft)
        {
            foreach (var scenario in BuildCraftScenarios(craft))
            {
                yield return new object[] { scenario };
            }
        }

        public static IEnumerable<ScenarioCase> BuildCraftScenarios(string craft)
        {
            var entries = LoadCraftEntries(craft);
            var crestId = entries.FirstOrDefault(entry => entry.Type.Equals("Crest", StringComparison.OrdinalIgnoreCase))?.Id;
            foreach (var entry in entries)
            {
                yield return BuildScenario(entry, craft, crestId);
            }
        }

        private static ScenarioCase BuildScenario(CardEntry entry, string craft, int? crestId)
        {
            var actions = new List<ScenarioAction>();
            var expectations = new List<IScenarioExpectation>();
            var graveyardCount = entry.EffectTypes.Contains("Necromancy") ? 8 : (int?)null;
            var bloodPool = entry.EffectTypes.Contains("Sacrifice") ? 3 : (int?)null;
            var setup = new ScenarioSetup
            {
                ActivePlayer = ScenarioPlayer.Player1,
                FirstPlayer = ScenarioPlayer.Player1,
                Player1 = new ScenarioPlayerSetup
                {
                    MaxPlayPoints = Math.Max(entry.Cost, 1),
                    CurrentPlayPoints = Math.Max(entry.Cost, 1),
                    GraveyardCount = graveyardCount,
                    BloodPool = bloodPool,
                    TurnNumber = DefaultTurnNumber
                },
                Player2 = new ScenarioPlayerSetup
                {
                    TurnNumber = DefaultTurnNumber
                },
                Cards = new List<ScenarioCardSpec>()
            };

            var focusKey = "focus";

            var needsPlay = entry.Type is "Follower" or "Amulet" or "Spell";
            var needsAct = entry.TriggerEvents.Contains("Act") || entry.AbilityKeywords.Contains("Act");
            var needsEvolve = entry.TriggerEvents.Contains("Evolve");
            var needsSuperEvolve = entry.TriggerEvents.Contains("SuperEvolve");
            var needsTurnStart = entry.TriggerEvents.Contains("TurnStart");
            var needsTurnEnd = entry.TriggerEvents.Contains("TurnEnd");
            var needsCountdownTick = entry.TriggerEvents.Contains("CountdownTick") || entry.Keywords.Contains("Countdown");
            var needsDestroy = entry.TriggerEvents.Contains("Destroy") || entry.AbilityKeywords.Contains("Lastword") || entry.AbilityKeywords.Contains("LastWords");
            var needsReturn = entry.TriggerEvents.Contains("Return");
            var needsTuck = entry.TriggerEvents.Contains("Tuck");
            var needsVanish = entry.TriggerEvents.Contains("Vanish");
            var needsAttack = entry.TriggerEvents.Contains("Attack") || entry.TriggerEvents.Contains("Clash");

            if (needsEvolve && needsSuperEvolve)
            {
                needsSuperEvolve = false;
            }

            if (needsEvolve)
            {
                setup.Player1.MaxEvolutionPoints = 1;
                setup.Player1.CurrentEvolutionPoints = 1;
                setup.Player1.CanEvolve = true;
            }

            if (needsSuperEvolve)
            {
                setup.Player1.MaxSuperEvolutionPoints = 1;
                setup.Player1.CurrentSuperEvolutionPoints = 1;
                setup.Player1.CanSuperEvolve = true;
                setup.Player1.TurnNumber = Math.Max(setup.Player1.TurnNumber, 7);
            }

            if (entry.Type == "Crest")
            {
                setup.Cards.Add(new ScenarioCardSpec
                {
                    Key = focusKey,
                    CardId = entry.Id,
                    Owner = ScenarioPlayer.Player1,
                    Zone = CardZone.Emblem
                });
                needsPlay = false;
            }
            else
            {
                setup.Cards.Add(new ScenarioCardSpec
                {
                    Key = focusKey,
                    CardId = entry.Id,
                    Owner = ScenarioPlayer.Player1,
                    Zone = CardZone.Hand
                });
            }

            var targetExpressions = entry.TargetExpressions;
            var needsEnemyTarget = targetExpressions.Any(expr => expr.Contains("opponent.field", StringComparison.OrdinalIgnoreCase));
            var needsEnemyAttacked = targetExpressions.Any(expr => expr.Contains("AttacksThisTurn", StringComparison.OrdinalIgnoreCase));
            var needsEnemySameName = targetExpressions.Any(expr => expr.Contains("AbilityTargets.target1", StringComparison.OrdinalIgnoreCase));
            var needsEnemySuperEvolved = targetExpressions.Any(expr =>
                expr.Contains("opponent.field", StringComparison.OrdinalIgnoreCase)
                && expr.Contains("IsSuperEvolved", StringComparison.OrdinalIgnoreCase)
                && (expr.Contains("== true", StringComparison.OrdinalIgnoreCase)
                    || expr.Contains("==true", StringComparison.OrdinalIgnoreCase)));
            var needsDeckQuery = targetExpressions.Any(expr => expr.Contains("deck.query", StringComparison.OrdinalIgnoreCase));
            var needsDeckFollower = targetExpressions.Any(expr =>
                expr.Contains("deck.query", StringComparison.OrdinalIgnoreCase)
                && expr.Contains("Type == \"Follower\"", StringComparison.OrdinalIgnoreCase));
            var needsDeckAmulet = targetExpressions.Any(expr =>
                expr.Contains("deck.query", StringComparison.OrdinalIgnoreCase)
                && expr.Contains("Type == \"Amulet\"", StringComparison.OrdinalIgnoreCase));
            var needsDeckSpell = targetExpressions.Any(expr =>
                expr.Contains("deck.query", StringComparison.OrdinalIgnoreCase)
                && expr.Contains("Type == \"Spell\"", StringComparison.OrdinalIgnoreCase));
            var needsDeckForestcraft = targetExpressions.Any(expr =>
                expr.Contains("deck.query", StringComparison.OrdinalIgnoreCase)
                && expr.Contains("Class == \"Forestcraft\"", StringComparison.OrdinalIgnoreCase));
            var needsDeckFairy = targetExpressions.Any(expr =>
                expr.Contains("deck.query", StringComparison.OrdinalIgnoreCase)
                && expr.Contains("妖精", StringComparison.OrdinalIgnoreCase));
            var needsDeckCostBelowTwo = targetExpressions.Any(expr =>
                expr.Contains("deck.query", StringComparison.OrdinalIgnoreCase)
                && (expr.Contains("Cost < 2", StringComparison.OrdinalIgnoreCase)
                    || expr.Contains("Cost <= 1", StringComparison.OrdinalIgnoreCase)
                    || expr.Contains("Cost == 1", StringComparison.OrdinalIgnoreCase)));
            var needsAllyFieldTarget = targetExpressions.Any(expr =>
                (expr.Contains("owner.field", StringComparison.OrdinalIgnoreCase)
                 || expr.Contains("ally.field", StringComparison.OrdinalIgnoreCase)));
            var needsAllyAmulet = targetExpressions.Any(expr => expr.Contains("Type == \"Amulet\"", StringComparison.OrdinalIgnoreCase));
            var needsAllyFairy = targetExpressions.Any(expr => expr.Contains("contains(self.Tribe, \"妖精\")", StringComparison.OrdinalIgnoreCase));
            var needsAllySoldier = targetExpressions.Any(expr => expr.Contains("contains(self.Tribe, \"士兵\")", StringComparison.OrdinalIgnoreCase));
            var needsAllyCostOne = targetExpressions.Any(expr => expr.Contains("self.Cost == 1", StringComparison.OrdinalIgnoreCase));
            var needsHandFollower = targetExpressions.Any(expr => expr.Contains("owner.hand", StringComparison.OrdinalIgnoreCase));
            var needsCrestFromGraveyard = targetExpressions.Any(expr => expr.Contains("graveyard.query(self.Type == \"Crest\")", StringComparison.OrdinalIgnoreCase));

            if (needsEnemyTarget)
            {
                if (needsEnemySameName)
                {
                    setup.Cards.Add(new ScenarioCardSpec
                    {
                        Key = "enemyA",
                        CardId = DummyNeutralFollowerId,
                        Owner = ScenarioPlayer.Player2,
                        Zone = CardZone.Field,
                        AttacksThisTurn = needsEnemyAttacked ? 1 : null,
                        IsSuperEvolved = needsEnemySuperEvolved ? true : null,
                        RegisterAbilities = false
                    });
                    setup.Cards.Add(new ScenarioCardSpec
                    {
                        Key = "enemyB",
                        CardId = DummyNeutralFollowerId,
                        Owner = ScenarioPlayer.Player2,
                        Zone = CardZone.Field,
                        IsSuperEvolved = needsEnemySuperEvolved ? true : null,
                        RegisterAbilities = false
                    });
                }
                else
                {
                    setup.Cards.Add(new ScenarioCardSpec
                    {
                        Key = "enemy",
                        CardId = DummyNeutralFollowerId,
                        Owner = ScenarioPlayer.Player2,
                        Zone = CardZone.Field,
                        AttacksThisTurn = needsEnemyAttacked ? 1 : null,
                        IsSuperEvolved = needsEnemySuperEvolved ? true : null,
                        RegisterAbilities = false
                    });
                }
            }

            if (needsAttack)
            {
                var attackTargetKey = needsEnemySameName ? "enemyA" : "enemy";
                if (!setup.Cards.Any(card => card.Key.Equals(attackTargetKey, StringComparison.OrdinalIgnoreCase)))
                {
                    setup.Cards.Add(new ScenarioCardSpec
                    {
                        Key = attackTargetKey,
                        CardId = DummyNeutralFollowerId,
                        Owner = ScenarioPlayer.Player2,
                        Zone = CardZone.Field,
                        RegisterAbilities = false
                    });
                }
            }

            if (needsAllyFieldTarget)
            {
                var allyCardId = DummyNeutralFollowerId;
                if (needsAllyAmulet)
                {
                    allyCardId = DummyNeutralAmuletId;
                }
                else if (needsAllyFairy)
                {
                    allyCardId = DummyFairyId;
                }
                else if (needsAllySoldier)
                {
                    allyCardId = DummySoldierId;
                }
                else if (needsAllyCostOne)
                {
                    allyCardId = DummyFairyId;
                }

                setup.Cards.Add(new ScenarioCardSpec
                {
                    Key = "ally",
                    CardId = allyCardId,
                    Owner = ScenarioPlayer.Player1,
                    Zone = CardZone.Field,
                    RegisterAbilities = false
                });
            }

            if (needsHandFollower)
            {
                setup.Cards.Add(new ScenarioCardSpec
                {
                    Key = "handTarget",
                    CardId = DummyHandFollowerId,
                    Owner = ScenarioPlayer.Player1,
                    Zone = CardZone.Hand,
                    RegisterAbilities = false
                });
            }

            if (needsCrestFromGraveyard && crestId.HasValue)
            {
                setup.Cards.Add(new ScenarioCardSpec
                {
                    Key = "crestGraveyard",
                    CardId = crestId.Value,
                    Owner = ScenarioPlayer.Player1,
                    Zone = CardZone.Graveyard,
                    RegisterAbilities = false
                });
            }

            if (entry.EffectTypes.Contains("Draw") || entry.EffectTypes.Contains("GetToken"))
            {
                var deckCardId = DummyNeutralFollowerId;
                if (needsDeckQuery)
                {
                    if (needsDeckAmulet)
                    {
                        deckCardId = DummyNeutralAmuletId;
                    }
                    else if (needsDeckForestcraft || needsDeckFairy || needsDeckCostBelowTwo)
                    {
                        deckCardId = DummyFairyId;
                    }
                    else if (needsDeckSpell)
                    {
                        deckCardId = DummyNeutralFollowerId;
                    }
                }

                setup.Cards.Add(new ScenarioCardSpec
                {
                    Key = "deckCard",
                    CardId = deckCardId,
                    Owner = ScenarioPlayer.Player1,
                    Zone = CardZone.Deck,
                    RegisterAbilities = false
                });
            }

            if (needsPlay)
            {
                actions.Add(new PlayCardAction(ScenarioPlayer.Player1, focusKey));
            }

            if (needsAct)
            {
                actions.Add(new ActAction(ScenarioPlayer.Player1, focusKey));
            }

            if (needsEvolve)
            {
                actions.Add(new EvolveAction(ScenarioPlayer.Player1, focusKey));
            }

            if (needsSuperEvolve)
            {
                actions.Add(new SuperEvolveAction(ScenarioPlayer.Player1, focusKey));
            }

            if (needsTurnStart)
            {
                actions.Add(new TurnStartAction(ScenarioPlayer.Player1));
            }

            if (needsCountdownTick && entry.Type is "Amulet" or "Crest" && actions.All(action => action is not TurnStartAction))
            {
                actions.Add(new CountdownTickAction(ScenarioPlayer.Player1, focusKey));
            }

            if (needsAttack && entry.Type == "Follower")
            {
                var hasImmediateAttack = entry.Keywords.Contains("Rush")
                    || entry.Keywords.Contains("Storm")
                    || needsEvolve
                    || needsSuperEvolve;

                if (!hasImmediateAttack && actions.All(action => action is not TurnStartAction))
                {
                    actions.Add(new TurnStartAction(ScenarioPlayer.Player1));
                }

                var attackTargetKey = needsEnemySameName ? "enemyA" : "enemy";
                actions.Add(new AttackAction(ScenarioPlayer.Player1, focusKey, attackTargetKey));
            }

            var leaveActions = new List<ScenarioAction>();
            if (entry.Type is "Follower" or "Amulet")
            {
                if (needsDestroy)
                {
                    leaveActions.Add(new DestroyAction(ScenarioPlayer.Player1, focusKey));
                }
                if (needsReturn)
                {
                    leaveActions.Add(new ReturnAction(ScenarioPlayer.Player1, focusKey));
                }
                if (needsTuck)
                {
                    leaveActions.Add(new TuckAction(ScenarioPlayer.Player1, focusKey));
                }
                if (needsVanish)
                {
                    leaveActions.Add(new VanishAction(ScenarioPlayer.Player1, focusKey));
                }
            }

            if (needsTurnEnd && leaveActions.Count == 0)
            {
                actions.Add(new TurnEndAction(ScenarioPlayer.Player1));
            }

            if (leaveActions.Count > 1)
            {
                leaveActions = new List<ScenarioAction> { leaveActions[0] };
            }

            actions.AddRange(leaveActions);

            var expectedZone = ResolveExpectedZone(entry, actions);
            if (expectedZone.HasValue)
            {
                expectations.Add(new ExpectCardZone(focusKey, expectedZone.Value));
            }

            foreach (var eventType in GetExpectedEventTypes(entry, actions))
            {
                expectations.Add(new ExpectEventTypeOccurred(eventType));
            }

            return new ScenarioCase
            {
                Id = $"{craft}-{entry.Id}-auto",
                Name = $"{craft} {entry.Id} {entry.Name}",
                FocusCardId = entry.Id,
                Setup = setup,
                Actions = actions,
                Expectations = expectations
            };
        }

        private static CardZone? ResolveExpectedZone(CardEntry entry, List<ScenarioAction> actions)
        {
            var countdownTick = actions.OfType<CountdownTickAction>().FirstOrDefault();
            if (countdownTick != null && entry.Countdown.HasValue)
            {
                var remaining = entry.Countdown.Value + countdownTick.ChangeAmount;
                if (remaining <= 0)
                {
                    return CardZone.Graveyard;
                }
            }

            if (actions.Any(action => action is ActAction) && entry.HasActDestroySelf)
            {
                return CardZone.Graveyard;
            }

            var hasExplicitLeaveAction = actions.Any(action =>
                action is DestroyAction or ReturnAction or TuckAction or VanishAction);
            if (actions.Any(action => action is AttackAction) && !hasExplicitLeaveAction)
            {
                return null;
            }

            var lastAction = actions.LastOrDefault();
            if (lastAction is DestroyAction)
            {
                if (entry.EffectTypes.Contains("Vanish") || entry.EffectTypes.Contains("ReplaceEvent"))
                {
                    return CardZone.Removed;
                }

                return CardZone.Graveyard;
            }

            if (lastAction is ReturnAction)
            {
                return CardZone.Hand;
            }

            if (lastAction is TuckAction)
            {
                return CardZone.Deck;
            }

            if (lastAction is VanishAction)
            {
                return CardZone.Removed;
            }

            return entry.Type switch
            {
                "Spell" => CardZone.Graveyard,
                "Crest" => CardZone.Emblem,
                "Amulet" => CardZone.Field,
                "Follower" => CardZone.Field,
                _ => null
            };
        }

        private static List<CardEntry> LoadCraftEntries(string craft)
        {
            var dataRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data"));
            var path = FindCraftDataPath(craft, dataRoot);
            if (path == null)
            {
                throw new FileNotFoundException($"Card data file not found for craft '{craft}' in {dataRoot} or {Path.Combine(dataRoot, "神眷的棋盘")}.");
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var entries = new List<CardEntry>();

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var id = element.GetProperty("id").GetInt32();
                var name = element.GetProperty("name").GetString() ?? string.Empty;
                var type = element.GetProperty("type").GetString() ?? "Follower";
                var cost = element.TryGetProperty("cost", out var costProp) ? costProp.GetInt32() : 0;
                var countdown = element.TryGetProperty("countdown", out var countdownProp) && countdownProp.ValueKind == JsonValueKind.Number
                    ? countdownProp.GetInt32()
                    : (int?)null;

                var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (element.TryGetProperty("keywords", out var keywordProp) && keywordProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var kw in keywordProp.EnumerateArray())
                    {
                        if (kw.ValueKind == JsonValueKind.String)
                        {
                            keywords.Add(kw.GetString() ?? string.Empty);
                        }
                    }
                }

                var abilities = new List<AbilityEntry>();
                if (element.TryGetProperty("abilities", out var abilitiesProp) && abilitiesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var abilityElement in abilitiesProp.EnumerateArray())
                    {
                        abilities.Add(ParseAbility(abilityElement));
                    }
                }

                entries.Add(new CardEntry(id, name, type, cost, countdown, keywords, abilities));
            }

            return entries;
        }

        private static string? FindCraftDataPath(string craft, string? dataRoot = null)
        {
            dataRoot ??= Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data"));
            var candidates = new[]
            {
                Path.Combine(dataRoot, "神眷的棋盘", $"card_data_{craft}.json"),
                Path.Combine(dataRoot, $"card_data_{craft}.json")
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static AbilityEntry ParseAbility(JsonElement abilityElement)
        {
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (abilityElement.TryGetProperty("abilityKeywords", out var keywordsProp) && keywordsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var kw in keywordsProp.EnumerateArray())
                {
                    switch (kw.ValueKind)
                    {
                        case JsonValueKind.String:
                            keywords.Add(kw.GetString() ?? string.Empty);
                            break;
                        case JsonValueKind.Object:
                            if (kw.TryGetProperty("keyword", out var keywordProp) && keywordProp.ValueKind == JsonValueKind.String)
                            {
                                keywords.Add(keywordProp.GetString() ?? string.Empty);
                            }
                            break;
                    }
                }
            }

            var triggerEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (abilityElement.TryGetProperty("trigger", out var triggerProp)
                && triggerProp.ValueKind == JsonValueKind.Object
                && triggerProp.TryGetProperty("eventTypes", out var eventTypesProp)
                && eventTypesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var evt in eventTypesProp.EnumerateArray())
                {
                    if (evt.ValueKind == JsonValueKind.String)
                    {
                        triggerEvents.Add(evt.GetString() ?? string.Empty);
                    }
                }
            }

            var effectTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (abilityElement.TryGetProperty("effects", out var effectsProp) && effectsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var effect in effectsProp.EnumerateArray())
                {
                    if (effect.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
                    {
                        effectTypes.Add(typeProp.GetString() ?? string.Empty);
                    }
                }
            }

            var targetExpressions = new List<string>();
            CollectTargetExpressions(abilityElement, targetExpressions);

            var hasConditions = abilityElement.TryGetProperty("conditions", out var conditionsProp)
                && conditionsProp.ValueKind == JsonValueKind.Array
                && conditionsProp.GetArrayLength() > 0;

            return new AbilityEntry(keywords, triggerEvents, effectTypes, targetExpressions, hasConditions);
        }

        private static void CollectTargetExpressions(JsonElement abilityElement, List<string> targetExpressions)
        {
            if (abilityElement.TryGetProperty("targets", out var targetsProp) && targetsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var target in targetsProp.EnumerateArray())
                {
                    if (target.TryGetProperty("query", out var queryProp) && queryProp.ValueKind == JsonValueKind.Object)
                    {
                        if (queryProp.TryGetProperty("sourcePoolExpression", out var sourceExpr) && sourceExpr.ValueKind == JsonValueKind.String)
                        {
                            targetExpressions.Add(sourceExpr.GetString() ?? string.Empty);
                        }
                        if (queryProp.TryGetProperty("targetPoolExpression", out var targetExpr) && targetExpr.ValueKind == JsonValueKind.String)
                        {
                            targetExpressions.Add(targetExpr.GetString() ?? string.Empty);
                        }
                    }
                }
            }

            if (abilityElement.TryGetProperty("effects", out var effectsProp) && effectsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var effect in effectsProp.EnumerateArray())
                {
                    if (effect.TryGetProperty("chainedAbilities", out var chainedProp) && chainedProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var chained in chainedProp.EnumerateArray())
                        {
                            CollectTargetExpressions(chained, targetExpressions);
                        }
                    }
                }
            }
        }

        private static HashSet<EventType> GetExpectedEventTypes(CardEntry entry, IReadOnlyList<ScenarioAction> actions)
        {
            var expected = new HashSet<EventType>();
            var scheduledTriggers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var action in actions)
            {
                switch (action)
                {
                    case PlayCardAction:
                        scheduledTriggers.Add("PlayCard");
                        break;
                    case ActAction:
                        scheduledTriggers.Add("Act");
                        break;
                    case EvolveAction:
                        scheduledTriggers.Add("Evolve");
                        break;
                    case SuperEvolveAction:
                        scheduledTriggers.Add("SuperEvolve");
                        break;
                    case TurnStartAction:
                        scheduledTriggers.Add("TurnStart");
                        break;
                    case TurnEndAction:
                        scheduledTriggers.Add("TurnEnd");
                        break;
                    case CountdownTickAction:
                        scheduledTriggers.Add("CountdownTick");
                        break;
                    case DestroyAction:
                        scheduledTriggers.Add("Destroy");
                        break;
                    case ReturnAction:
                        scheduledTriggers.Add("Return");
                        break;
                    case TuckAction:
                        scheduledTriggers.Add("Tuck");
                        break;
                    case VanishAction:
                        scheduledTriggers.Add("Vanish");
                        break;
                }
            }

            foreach (var ability in entry.Abilities)
            {
                if (ability.HasConditions)
                {
                    continue;
                }

                var triggers = ability.TriggerEvents;
                var triggered = triggers.Count == 0 && scheduledTriggers.Contains("PlayCard")
                    || triggers.Any(evt => scheduledTriggers.Contains(evt));

                if (!triggered)
                {
                    continue;
                }

                foreach (var effectType in ability.EffectTypes)
                {
                    if (EffectEventMap.TryGetValue(effectType, out var events))
                    {
                        foreach (var evt in events)
                        {
                            expected.Add(evt);
                        }
                    }
                }
            }

            return expected;
        }

        private sealed class CardEntry
        {
            public int Id { get; }
            public string Name { get; }
            public string Type { get; }
            public int Cost { get; }
            public int? Countdown { get; }
            public HashSet<string> Keywords { get; }
            public List<AbilityEntry> Abilities { get; }
            public HashSet<string> AbilityKeywords { get; }
            public HashSet<string> TriggerEvents { get; }
            public HashSet<string> EffectTypes { get; }
            public List<string> TargetExpressions { get; }
            public bool RequiresEnemyTarget { get; }
            public bool RequiresAllyTarget { get; }
            public bool HasActDestroySelf { get; }

            public CardEntry(int id, string name, string type, int cost, int? countdown, HashSet<string> keywords, List<AbilityEntry> abilities)
            {
                Id = id;
                Name = name;
                Type = type;
                Cost = cost;
                Countdown = countdown;
                Keywords = keywords;
                Abilities = abilities;

                AbilityKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                TriggerEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                EffectTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                TargetExpressions = new List<string>();

                foreach (var ability in Abilities)
                {
                    foreach (var keyword in ability.AbilityKeywords)
                    {
                        AbilityKeywords.Add(keyword);
                    }

                    foreach (var trigger in ability.TriggerEvents)
                    {
                        TriggerEvents.Add(trigger);
                    }

                    foreach (var effectType in ability.EffectTypes)
                    {
                        EffectTypes.Add(effectType);
                    }

                    TargetExpressions.AddRange(ability.TargetExpressions);
                }

                RequiresEnemyTarget = TargetExpressions.Any(expr =>
                    expr.Contains("enemy", StringComparison.OrdinalIgnoreCase)
                    && expr.Contains("field", StringComparison.OrdinalIgnoreCase));

                RequiresAllyTarget = TargetExpressions.Any(expr =>
                    (expr.Contains("owner", StringComparison.OrdinalIgnoreCase)
                     || expr.Contains("ally", StringComparison.OrdinalIgnoreCase))
                    && expr.Contains("field", StringComparison.OrdinalIgnoreCase));

                HasActDestroySelf = Abilities.Any(ability =>
                    ability.AbilityKeywords.Contains("Act")
                    && ability.EffectTypes.Contains("Destroy")
                    && ability.TargetExpressions.Any(expr => expr.Contains("context.sourceCard", StringComparison.OrdinalIgnoreCase)));
            }
        }

        private sealed class AbilityEntry
        {
            public HashSet<string> AbilityKeywords { get; }
            public HashSet<string> TriggerEvents { get; }
            public HashSet<string> EffectTypes { get; }
            public List<string> TargetExpressions { get; }
            public bool HasConditions { get; }

            public AbilityEntry(HashSet<string> abilityKeywords, HashSet<string> triggerEvents, HashSet<string> effectTypes, List<string> targetExpressions, bool hasConditions)
            {
                AbilityKeywords = abilityKeywords;
                TriggerEvents = triggerEvents;
                EffectTypes = effectTypes;
                TargetExpressions = targetExpressions;
                HasConditions = hasConditions;
            }
        }
    }
}
