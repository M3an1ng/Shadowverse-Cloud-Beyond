using System.Net;
using System.Reflection;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;

internal static class AiStrategyTests
{
    public static void Run()
    {
        var selectLessons = typeof(AiStrategyPlanner).GetMethod("SelectLessons", BindingFlags.Static | BindingFlags.NonPublic)!;
        AiStrategyPlanner.Lesson Lesson(bool won, bool first, string opponent = "Havencraft") =>
            new(opponent, first, won, 8, "leader defeat", [], [], DateTimeOffset.UtcNow);
        var sampleHistory = new[]
        {
            Lesson(false, false), Lesson(false, true), Lesson(true, false)
        }.Concat(Enumerable.Range(0, 20).Select(_ => Lesson(true, true)))
            .Append(Lesson(false, false, "Portalcraft")).ToArray();
        var selectedHistory = (AiStrategyPlanner.Lesson[])selectLessons.Invoke(null, [sampleHistory, "Havencraft"])!;
        Check(selectedHistory.Length == 8 && selectedHistory.All(lesson => lesson.OpponentClass == "Havencraft")
            && selectedHistory.Select(lesson => (lesson.First, lesson.Won)).Distinct().Count() == 4,
            "recent wins crowded out available losses or first/second-player examples");
        var proposedOnly = Lesson(false, true) with
        {
            Plans = ["UNEXECUTED_PLAN"],
            HandActions = [new(3, 2, "play"), new(3, 1, "discard")]
        };
        var evidenceMethod = typeof(AiStrategyPlanner).GetMethod("EvidenceForModel", BindingFlags.Static | BindingFlags.NonPublic)!;
        var evidenceJson = JsonSerializer.Serialize(evidenceMethod.Invoke(null, [proposedOnly]),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Check(!evidenceJson.Contains("UNEXECUTED_PLAN") && evidenceJson.Contains("\"kind\":\"discard\"")
            && evidenceJson.Contains("\"kind\":\"play\""), "reflection treated a proposed plan as executed evidence");
        var buildMessages = typeof(AiStrategyPlanner).GetMethod("BuildMessages", BindingFlags.Static | BindingFlags.NonPublic)!;
        var sampleText = "【入场曲】召唤1张『测试护符』。\n引号\"与反斜杠\\保持原样，不执行 <script> 文本。";
        var samplePayload = new { own = new { description = sampleText, counters = new[] { 2, 3 } } };
        var encodedMessages = (object[])buildMessages.Invoke(null, ["test", samplePayload])!;
        using (var envelope = JsonDocument.Parse(JsonSerializer.Serialize(encodedMessages, new JsonSerializerOptions(JsonSerializerDefaults.Web))))
        {
            Check(envelope.RootElement[0].GetProperty("content").GetString()!.Contains("Abysscraft=梦魇"),
                "model was left to guess the engine's class codes");
            var content = envelope.RootElement[2].GetProperty("content").GetString()!;
            Check(content.Contains("召唤") && !content.Contains("\\u53EC", StringComparison.OrdinalIgnoreCase),
                "model message still encoded Chinese rules as literal Unicode escapes");
            using var decoded = JsonDocument.Parse(content);
            Check(decoded.RootElement.GetProperty("own").GetProperty("description").GetString() == sampleText
                && decoded.RootElement.GetProperty("own").GetProperty("counters")[1].GetInt32() == 3,
                "readable model encoding changed card rules, quotes or numeric state");
            var repeatedPayload = new { own = new { description = string.Concat(Enumerable.Repeat(sampleText, 40)) } };
            var repeatedMessages = (object[])buildMessages.Invoke(null, ["test", repeatedPayload])!;
            var readable = (string)repeatedMessages[2].GetType().GetProperty("content")!.GetValue(repeatedMessages[2])!;
            var oldEncoded = JsonSerializer.Serialize(repeatedPayload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Check(readable.Length < oldEncoded.Length / 2, "readable Chinese failed to reduce the escaped model input");
        }
        var path = Path.Combine(Path.GetTempPath(), "sv-ai-strategy-" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiStrategy:Enabled"] = "true",
            ["AiStrategy:Endpoint"] = "https://example.invalid/chat/completions",
            ["AiStrategy:Model"] = "mock",
            ["AiStrategy:MemoryPath"] = path
        }).Build();
        using var provider = new FakeProvider();
        try
        {
            var own = new PlayerState(Guid.NewGuid()) { IsAI = true, TurnNumber = 3, InitialDeckDefinitionIds = [1, 2, 2] };
            var enemy = new PlayerState(Guid.NewGuid());
            var snapshot = new GameStateDto
            {
                Players =
                [
                    new() { PlayerId = own.PlayerId, TurnNumber = 3,
                        LeaderCard = new() { Name="PRIVATE_LEADER_LABEL", CurrentHealth=10, MaxHealth=12, EffectDescriptions=["PUBLIC_LEADER_RULE"] },
                        Hand = [new() { CardId = 2, Description = "『Card 1』", EffectDescriptions = ["hand discard when playing another card"], Counters = new() { ["spellboost"] = 3 }, ActAbilities = [new() { Name = "Activate", Description = "draw a card" }] }] },
                    new()
                    {
                        PlayerId = enemy.PlayerId,
                        Hand = [new() { Name = "opponent-hidden-hand" }],
                        Deck = [new() { Name = "opponent-hidden-deck" }],
                        DisplayName = "opponent-account-name"
                    }
                ]
            };
            var blockedCard = Guid.NewGuid();
            snapshot.Players[0].Hand.Add(new CardInstanceDto { CardId = 1, InstanceId = blockedCard });
            Task<PlayabilityResultDto> Legal(Guid uid, CancellationToken _) => Task.FromResult(new PlayabilityResultDto
            {
                IsPlayable = uid != blockedCard,
                SelectedPlayMode = PlayMode.Accelerate,
                PlayModes = [new() { Mode = PlayMode.Accelerate, EffectiveCost = 2, IsAvailable = true }]
            });
            var game = new GameState("strategy-one", own, enemy, own.PlayerId);
            using (var planner = new AiStrategyPlanner(provider, config, new TestHost(), NullLogger<AiStrategyPlanner>.Instance))
            {
                planner.WarmDeckAsync(game, own.PlayerId, enemy.Class.ToString())?.GetAwaiter().GetResult();
                var plan = planner.PlanAsync(game, snapshot, own.PlayerId, default, Legal).GetAwaiter().GetResult();
                Check(plan?.Prioritize.SequenceEqual(new[] { 2 }) == true && plan.Hold.Length == 0 && plan.Ability == null, "unknown hand IDs survived");
                planner.PlanAsync(game, snapshot, own.PlayerId, default, Legal).GetAwaiter().GetResult();
                Check(plan?.Plan == "play Card 2", "card ID did not resolve to canonical name");
                var currentPlan = typeof(AiStrategyPlanner).GetMethod("GetCurrentPlan", BindingFlags.Instance | BindingFlags.NonPublic)!;
                Check(ReferenceEquals(currentPlan.Invoke(planner, [game, own.PlayerId]), plan)
                    && currentPlan.Invoke(planner, [game, enemy.PlayerId]) == null,
                    "material guidance used another player's plan");
                own.TurnNumber += 2;
                Check(currentPlan.Invoke(planner, [game, own.PlayerId]) == null,
                    "expired model plan continued protecting obsolete materials");
                own.TurnNumber -= 2;
                var preferred = new CardInstanceDto { InstanceId = Guid.NewGuid(), CardId = 2, OwnerPlayerId = own.PlayerId, CurrentCost = 8, Type = CardType.Follower };
                var expendable = preferred with { InstanceId = Guid.NewGuid(), CardId = 1 };
                var materialSnapshot = snapshot with { Players = [snapshot.Players[0] with { Hand = [preferred, expendable], MaxPP = 4,
                    LeaderCard = new() { CurrentHealth = 20 } }, snapshot.Players[1] with { Field = [] }] };
                var fusionAbility = Proxy<IAbility>.Make((method, args) => method.Name switch
                {
                    "HasKeyword" => args![0]!.ToString() == "Fusion",
                    "get_SourceCard" => new CardInstance(Guid.NewGuid(), 2, own.PlayerId),
                    _ => null
                });
                game.AbilityManager = Proxy<IAbilityManager>.Make((method, args) => method.Name == "GetAbilityByRuntimeId" ? fusionAbility : null);
                var fakeGameService = Proxy<IGameService>.Make((method, args) => method.Name == "GetGameState" ? game : null);
                using (var services = new ServiceCollection().AddSingleton(fakeGameService).AddSingleton(planner).BuildServiceProvider())
                {
                    var emptyModelConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
                        { ["AiModel:Path"] = Path.Combine(path, "missing-model.json") }).Build();
                    var local = new AiLearningModel(new TestHost(), emptyModelConfig, NullLogger<AiLearningModel>.Instance);
                    var service = new AiService(NullLogger<AiService>.Instance, services.GetRequiredService<IServiceScopeFactory>(), local);
                    var request = new PlayerChoiceRequest { GameId = game.GameId, RequestingPlayerId = own.PlayerId,
                        TargetKey = "materials", MinSelectionCount = 1, MaxSelectionCount = 1,
                        SelectableTargetIds = [preferred.InstanceId, expendable.InstanceId], SelectableCardInstances = [preferred, expendable] };
                    var materialChoice = service.DecideTargetChoiceAsync(request, materialSnapshot).GetAwaiter().GetResult();
                    Check(materialChoice.ChosenTargetIds.SequenceEqual([expendable.InstanceId]),
                        "actual fusion choice ignored the model's current priority piece");
                    own.TurnNumber += 2;
                    Check(service.DecideTargetChoiceAsync(request, materialSnapshot).GetAwaiter().GetResult().ChosenTargetIds.SequenceEqual([preferred.InstanceId]),
                        "actual fusion choice retained an expired plan");
                    own.TurnNumber -= 2;
                    var threatened = materialSnapshot with { Players = [materialSnapshot.Players[0] with { LeaderCard = new() { CurrentHealth = 1 } },
                        materialSnapshot.Players[1] with { Field = [new() { CurrentAttack = 1, CurrentHealth = 1, Type = CardType.Follower }] }] };
                    Check(service.DecideTargetChoiceAsync(request, threatened).GetAwaiter().GetResult().ChosenTargetIds.SequenceEqual([preferred.InstanceId]),
                        "material retention overrode a visible lethal emergency");
                }
                game.AbilityManager = null!;
                Check(provider.Bodies.Any(body => body.Contains("\"references\":{\"cards\":[{\"id\":1")), "generated card reference was not sent");
                Check(provider.Bodies.Any(body => body.Contains("\"legalPlays\":[{\"handIndex\":0,\"cardId\":2,\"mode\":\"Accelerate\",\"cost\":2")), "engine playability was not included");
                Check(provider.Bodies.Any(body => body.Contains("hand discard when playing another card")
                    && body.Contains("\"spellboost\":3") && body.Contains("draw a card")
                    && body.Contains("\"localModelScore\":null")), "visible granted rules or model-score availability omitted");
                using (var input = JsonDocument.Parse(provider.Bodies.Last()))
                {
                    var visible = input.RootElement.GetProperty("own");
                    Check(visible.GetProperty("maxHp").GetInt32() == 12
                        && visible.GetProperty("leader").GetProperty("maxHealth").GetInt32() == 12
                        && visible.GetProperty("leader").GetProperty("effects")[0].GetString() == "PUBLIC_LEADER_RULE",
                        "public leader effects or reduced maximum health were hidden from the model");
                    Check(!provider.Bodies.Any(body => body.Contains("PRIVATE_LEADER_LABEL"))
                        && snapshot.Players[0].LeaderCard!.Name == "PRIVATE_LEADER_LABEL",
                        "leader context exposed a personal label or mutated the visible snapshot");
                }
                using (var usage = JsonDocument.Parse(JsonSerializer.Serialize(planner.ProviderUsage)))
                    Check(usage.RootElement.GetProperty("promptTokens").GetInt64() == 2000
                        && usage.RootElement.GetProperty("cachedTokens").GetInt64() == 1200,
                        "provider cache token usage was not accumulated");
                Check(provider.Bodies.Count == 2, "duplicate model call on the same turn");
                var source = new CardInstance(Guid.NewGuid(), 2, own.PlayerId);
                var chooseMode = typeof(AiStrategyPlanner).GetMethod("ChooseFromOptionsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
                List<Guid>? Select(PlayerChoiceRequest request) =>
                    ((Task<List<Guid>?>)chooseMode.Invoke(planner, [game, snapshot, request, source, CancellationToken.None])!)
                        .GetAwaiter().GetResult();
                PlayerChoiceRequest MakeChoice(string name)
                {
                    var first = Guid.NewGuid();
                    var second = Guid.NewGuid();
                    return new PlayerChoiceRequest
                    {
                        RequestingPlayerId = own.PlayerId,
                        TargetKey = "decision",
                        MinSelectionCount = 1,
                        MaxSelectionCount = 1,
                        SelectableTargetIds = [first, second],
                        SelectableCardInstances = [new() { InstanceId = first, Name = name }, new() { InstanceId = second, Name = "Option 2" }]
                    };
                }
                var fullText = new string('文', 1700) + "TAIL_RULE_完整条件";
                var longChoice = MakeChoice(new string('模', 130) + "TAIL_NAME_关键分支");
                longChoice.SelectableCardInstances[0] = longChoice.SelectableCardInstances[0] with { Description = fullText };
                Check(Select(longChoice) != null && provider.Bodies.Last().Contains("TAIL_RULE_")
                    && provider.Bodies.Last().Contains("TAIL_NAME_"), "mode or card text was silently clipped before model selection");
                var choice = MakeChoice("Option 1");
                Check(Select(choice)?.SequenceEqual([choice.SelectableTargetIds[0]]) == true, "model mode not selected");
                Check(provider.Prefixes.Take(4).Distinct().Count() == 1,
                    "guide, turn and mode requests did not share a stable rules prefix");
                var sameChoice = MakeChoice("Option 1");
                Check(Select(sameChoice)?.SequenceEqual([sameChoice.SelectableTargetIds[0]]) == true
                    && provider.Bodies.Count == 4, "equivalent repeat did not remap cached index to current GUID");
                Check(Select(MakeChoice("Invalid")) == null, "illegal model index was accepted");
                var discover = MakeChoice("Option 1");
                discover.TargetKey = "discover";
                discover.SelectableCardInstances[0] = discover.SelectableCardInstances[0] with { CardId = 2, CurrentCost = 2 };
                Check(Select(discover)?.SequenceEqual([discover.SelectableTargetIds[0]]) == true
                    && provider.Bodies.Last().Contains("\"currentCost\":2"), "discover omitted legal card data");
                Check(Select(MakeChoice("Timeout")) == null, "timeout did not fall back");
                var callsAfterTimeout = provider.Bodies.Count;
                Check(Select(MakeChoice("Timeout")) == null && provider.Bodies.Count == callsAfterTimeout,
                    "equivalent failed choice triggered another provider call");
                var metrics = ((AiStrategyPlanner.DecisionMetrics Turn, AiStrategyPlanner.DecisionMetrics Choice))
                    typeof(AiStrategyPlanner).GetMethod("GetDecisionMetrics", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(planner, [game])!;
                Check(metrics.Turn.Attempts == 1 && metrics.Turn.Accepted == 1
                    && metrics.Choice.Attempts == 5 && metrics.Choice.Accepted == 3
                    && metrics.Choice.Invalid == 1 && metrics.Choice.TimedOut == 1,
                    "decision metrics omitted accepted, invalid or timeout outcomes");
                var cache = typeof(AiStrategyPlanner).GetField("matches", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(planner)!;
                object?[] lookup = [game, null];
                Check((bool)cache.GetType().GetMethod("TryGetValue")!.Invoke(cache, lookup)!, "missing match state");
                var matchState = lookup[1]!;
                matchState.GetType().GetField("Attempts")!.SetValue(matchState, 6);
                snapshot.Players[0].TurnNumber = 5;
                var callsBeforeExpiredPlan = provider.Bodies.Count;
                Check(planner.PlanAsync(game, snapshot, own.PlayerId, default, Legal).GetAwaiter().GetResult() == null
                    && provider.Bodies.Count == callsBeforeExpiredPlan, "expired turn plan survived the request cap");
                snapshot.Players[0].TurnNumber = 3;
                var committed = new ChooseEvent(choice.SelectableTargetIds, 1, source);
                typeof(ChooseEvent).GetMethod("SetSelectedOptionIndexes", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(committed, [new[] { 0 }]);
                planner.ObserveCommittedEvents(game, [committed, new GameEndEvent(own.PlayerId, enemy.PlayerId, "leader defeat")]);
            }
            using (var restarted = new AiStrategyPlanner(provider, config, new TestHost(), NullLogger<AiStrategyPlanner>.Instance))
            {
                var next = new GameState("strategy-two", own, enemy, own.PlayerId);
                restarted.WarmDeckAsync(next, own.PlayerId, enemy.Class.ToString())?.GetAwaiter().GetResult();
                restarted.PlanAsync(next, snapshot, own.PlayerId, default, Legal).GetAwaiter().GetResult();
            }
            Check(provider.Bodies.All(body => !body.Contains("opponent-hidden") && !body.Contains("opponent-account")), "private data sent");
            Check(provider.Bodies.Skip(4).Any(body => body.Contains("\"won\":true")
                && body.Contains("\"kind\":\"mode\"") && body.Contains("\"position\":{")
                && body.Contains("\"handSize\":2") && body.Contains("\"matchupGames\":1")),
                "committed choice or matchup summary was not reused");
            using (var choicesOnly = new AiStrategyPlanner(provider, config, new TestHost(), NullLogger<AiStrategyPlanner>.Instance))
            {
                var next = new GameState("strategy-choices", own, enemy, own.PlayerId);
                typeof(GameState).GetProperty("StrategyChoicesOnly", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(next, true);
                choicesOnly.WarmDeckAsync(next, own.PlayerId, enemy.Class.ToString())?.GetAwaiter().GetResult();
                var callsBefore = provider.Bodies.Count;
                Check(choicesOnly.PlanAsync(next, snapshot, own.PlayerId, default, Legal).GetAwaiter().GetResult() == null
                    && provider.Bodies.Count == callsBefore, "choices-only strategy requested a turn plan");

                var option = Guid.NewGuid();
                var other = Guid.NewGuid();
                var request = new PlayerChoiceRequest
                {
                    RequestingPlayerId = own.PlayerId, TargetKey = "discover",
                    MinSelectionCount = 1, MaxSelectionCount = 1, SelectableTargetIds = [option, other],
                    SelectableCardInstances = [new() { InstanceId = option, Name = "Option 1" }, new() { InstanceId = other, Name = "Option 2" }]
                };
                var method = typeof(AiStrategyPlanner).GetMethod("ChooseFromOptionsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var selected = ((Task<List<Guid>?>)method.Invoke(choicesOnly, [next, snapshot, request, null, CancellationToken.None])!)
                    .GetAwaiter().GetResult();
                Check(selected?.SequenceEqual([option]) == true && provider.Bodies.Count == callsBefore + 1
                    && provider.Bodies.Last().Contains("\"matchupGames\":1"), "choices-only strategy omitted a legal choice or prior experience");
            }
            using (var reflectionPlanner = new AiStrategyPlanner(provider, config, new TestHost(), NullLogger<AiStrategyPlanner>.Instance))
            {
                var queue = typeof(AiStrategyPlanner).GetMethod("QueueReflection", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var running = typeof(AiStrategyPlanner).GetField("guideRunning", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var evidence = Enumerable.Range(0, 4).Select(i => (Lesson(i % 2 == 0, i < 2) with { Played = [2] })).ToArray();
                var key = new string('a', 64);
                queue.Invoke(reflectionPlanner, [key, evidence]);
                Check(SpinWait.SpinUntil(() => !(bool)running.GetValue(reflectionPlanner)!, TimeSpan.FromSeconds(5)),
                    "background reflection did not finish");
                var reflectionPath = Path.Combine(path, key + ".reflection.json");
                var saved = JsonSerializer.Deserialize<AiStrategyPlanner.StrategyReflection>(File.ReadAllText(reflectionPath),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                Check(saved.Insights[0].Sources.SequenceEqual([0, 1]) && saved.Evidence.Length == 4,
                    "reflection lost source evidence");
                var compact = typeof(AiStrategyPlanner).GetMethod("ReflectionForModel", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, [saved, null]);
                using (var promptSummary = JsonDocument.Parse(JsonSerializer.Serialize(compact,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))))
                {
                    var summary = promptSummary.RootElement;
                    Check(summary.GetProperty("insights")[0].GetProperty("sources")[0].GetInt32() == 0
                        && summary.GetProperty("evidence").GetArrayLength() == 4
                        && !summary.GetProperty("evidence")[0].GetProperty("facts").TryGetProperty("plans", out _),
                        "summary duplicated full plans or lost evidence references");
                }
                var recent = new[] { saved.Evidence[1], saved.Evidence[0] }.Select(lesson =>
                    JsonSerializer.Deserialize<AiStrategyPlanner.Lesson>(JsonSerializer.Serialize(lesson))!).ToArray();
                var shared = typeof(AiStrategyPlanner).GetMethod("ReflectionForModel", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, [saved, recent]);
                using (var linked = JsonDocument.Parse(JsonSerializer.Serialize(shared, new JsonSerializerOptions(JsonSerializerDefaults.Web))))
                {
                    var rows = linked.RootElement.GetProperty("evidence");
                    Check(rows[0].GetProperty("recentLesson").GetInt32() == 1 && rows[1].GetProperty("recentLesson").GetInt32() == 0
                        && rows[0].GetProperty("facts").ValueKind == JsonValueKind.Null
                        && rows[2].GetProperty("recentLesson").ValueKind == JsonValueKind.Null,
                        "reordered recent lessons corrupted or duplicated reflection source references");
                    for (var index = 0; index < saved.Evidence.Length; index++)
                    {
                        var row = rows[index];
                        var pointer = row.GetProperty("recentLesson");
                        var actual = pointer.ValueKind == JsonValueKind.Null ? row.GetProperty("facts").GetRawText()
                            : JsonSerializer.Serialize(evidenceMethod.Invoke(null, [recent[pointer.GetInt32()]]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
                        var expected = JsonSerializer.Serialize(evidenceMethod.Invoke(null, [saved.Evidence[index]]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
                        Check(actual == expected, "sharing reflection evidence lost committed facts");
                    }
                }
                var rich = saved with { Evidence = saved.Evidence.Select(lesson => lesson with
                {
                    HandActions = Enumerable.Range(0,40).Select(index => new AiStrategyPlanner.HandActionFact(index / 4,2,index % 2 == 0 ? "play" : "discard")).ToArray()
                }).ToArray() };
                var reflectionMethod = typeof(AiStrategyPlanner).GetMethod("ReflectionForModel", BindingFlags.Static | BindingFlags.NonPublic)!;
                var fullBytes = JsonSerializer.SerializeToUtf8Bytes(reflectionMethod.Invoke(null, [rich, null])).Length;
                var sharedBytes = JsonSerializer.SerializeToUtf8Bytes(reflectionMethod.Invoke(null, [rich, rich.Evidence])).Length;
                Check(sharedBytes < fullBytes / 3, "shared evidence still retransmitted most overlapping event facts");
                Console.WriteLine($"PASS shared reflection source payload: {fullBytes} -> {sharedBytes} bytes; facts retained through source references");
                var calls = provider.Bodies.Count;
                queue.Invoke(reflectionPlanner, [key, evidence]);
                Check(provider.Bodies.Count == calls, "reflection regenerated without four new games");
                provider.InvalidReflection = true;
                var invalidKey = new string('b', 64);
                queue.Invoke(reflectionPlanner, [invalidKey, evidence]);
                Check(SpinWait.SpinUntil(() => !(bool)running.GetValue(reflectionPlanner)!, TimeSpan.FromSeconds(5))
                    && !File.Exists(Path.Combine(path, invalidKey + ".reflection.json")),
                    "reflection accepted an invented source");
            }
            using (var actionsPlanner = new AiStrategyPlanner(provider, config, new TestHost(), NullLogger<AiStrategyPlanner>.Instance))
            {
                var next = new GameState("strategy-ability", own, enemy, own.PlayerId);
                actionsPlanner.WarmDeckAsync(next, own.PlayerId, enemy.Class.ToString())?.GetAwaiter().GetResult();
                var candidate = new AiStrategyPlanner.LocalAbilityCandidate(2, "act", 1, "Draw", 3);
                var plan = actionsPlanner.PlanAsync(next, snapshot, own.PlayerId, default, Legal,
                    [candidate with { CardId = 1, Name = "Other", LocalScore = 3.5 }, candidate])
                    .GetAwaiter().GetResult();
                Check(plan?.Ability is { CardId: 2, Kind: "act", Cost: 1, Intent: "use" }
                    && provider.Bodies.Last().Contains("\"localAbilityCandidates\":[{"), "legal local ability recommendation was not integrated");
                var bias = typeof(AiStrategyPlanner.TurnPlan).GetMethod("AbilityBias", BindingFlags.Instance | BindingFlags.NonPublic)!;
                double Bias(AiStrategyPlanner.LocalAbilityCandidate action) => (double)bias.Invoke(plan, [action])!;
                Check(Bias(candidate) > 0 && Bias(candidate with { Cost = 2 }) == 0
                    && Bias(candidate with { Kind = "fusion" }) == 0 && Bias(candidate with { Name = "Other" }) == 0,
                    "ability recommendation survived a changed cost, kind or ability");
                var mismatch = new GameState("strategy-ability-mismatch", own, enemy, own.PlayerId);
                var rejected = actionsPlanner.PlanAsync(mismatch, snapshot, own.PlayerId, default, Legal,
                    [candidate with { Cost = 2 }]).GetAwaiter().GetResult();
                Check(rejected?.Ability == null, "model invented an unavailable ability cost");
                var setPending = typeof(AiStrategyPlanner).GetMethod("SetPendingAbility", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var setComparison = typeof(AiStrategyPlanner).GetMethod("SetPendingComparison", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var comparison = new AiStrategyPlanner.ActionComparisonFact(3,
                    new("PlayCard", 1, null, null, 3), new("RequestAct", 2, null, "Draw", 3.85));
                Check(comparison.Changed && !(comparison with { Selected = comparison.Local with { Score = 8 } }).Changed,
                    "decision comparison treated score-only differences as changed actions");
                setComparison.Invoke(actionsPlanner, [next, comparison]);
                setComparison.Invoke(actionsPlanner, [next, null]);
                var pendingAttack = typeof(AiStrategyPlanner).GetMethod("SetPendingAttack", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var striker = new CardInstance(Guid.NewGuid(), 2, own.PlayerId);
                var smallEnemy = new CardInstance(Guid.NewGuid(), 1, enemy.PlayerId);
                var attackFact = new AiStrategyPlanner.AttackFact(3, 2, 1, false, true, 3, 1, 1, 20, 12, 1, ["Ward"]);
                pendingAttack.Invoke(actionsPlanner, [next, striker.InstanceId, smallEnemy.InstanceId, attackFact]);
                actionsPlanner.ObserveCommittedEvents(next, [new AttackEvent(smallEnemy, striker)]);
                pendingAttack.Invoke(actionsPlanner, [next, Guid.Empty, Guid.Empty, null]);
                pendingAttack.Invoke(actionsPlanner, [next, striker.InstanceId, smallEnemy.InstanceId, attackFact]);
                actionsPlanner.ObserveCommittedEvents(next, [new AttackEvent(striker, smallEnemy), new AttackEvent(striker, smallEnemy)]);
                var runtimeId = Guid.NewGuid();
                setPending.Invoke(actionsPlanner, [next, runtimeId, candidate]);
                setComparison.Invoke(actionsPlanner, [next, comparison]);
                var activatedCard = new CardInstance(Guid.NewGuid(), 2, own.PlayerId);
                var fusedMaterial = new CardInstance(Guid.NewGuid(), 1, own.PlayerId);
                var enemyMaterial = new CardInstance(Guid.NewGuid(), 1, enemy.PlayerId);
                actionsPlanner.ObserveCommittedEvents(next,
                    [new FusionInvokedEvent(own, activatedCard)]);
                actionsPlanner.ObserveCommittedEvents(next,
                    [new FusionEvent(activatedCard, [fusedMaterial]), new FusionEvent(smallEnemy, [enemyMaterial])]);
                actionsPlanner.ObserveCommittedEvents(next,
                    [new ActInvokedEvent(own, activatedCard, 1, runtimeId), new GameEndEvent(own.PlayerId, enemy.PlayerId, "leader defeat")]);
                var historyMethod = typeof(AiStrategyPlanner).GetMethod("ReadHistory", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var history = (IReadOnlyList<AiStrategyPlanner.HistoryEntry>)historyMethod.Invoke(actionsPlanner, [])!;
                var materialFacts = history.SelectMany(entry => entry.Lesson.HandActions ?? []).Where(action => action.Kind == "fusion").ToArray();
                Check(materialFacts.Length == 1 && materialFacts[0].CardId == 1 && materialFacts[0].SourceCardId == 2,
                    "fusion material history confused invocation with consumption or recorded the opponent's hidden material");
                Check(history.Any(entry => entry.Lesson.AbilityActions?.Any(action =>
                    action.CardId == 2 && action.Kind == "act" && action.Cost == 1 && action.MatchesModelAdvice) == true),
                    "the final winning activated ability was omitted from committed experience");
                Check(history.Any(entry => entry.Lesson.ActionComparisons is { Length: 1 } comparisons
                    && comparisons[0].Changed && comparisons[0].Selected.Kind == "RequestAct"
                    && entry.Lesson.ProposedAbilities?.Any(item => item.LocalRank == 2) == true),
                    "committed final decision or non-top model advice was omitted from history");
                var compactLesson = typeof(AiStrategyPlanner).GetMethod("LessonForModel", BindingFlags.Static | BindingFlags.NonPublic)!;
                var retained = history.First(entry => entry.Lesson.ActionComparisons?.Length == 1).Lesson;
                Check(retained.Attacks is { Length: 1 } attacks && attacks[0].Storm && !attacks[0].Face
                    && attacks[0].TargetHealth == 1 && attacks[0].OwnHealth == 20 && attacks[0].TargetKeywords?.Single() == "Ward",
                    "attack facts were omitted, duplicated, or captured from a different attack");
                var repeated = retained with { ActionComparisons = Enumerable.Range(0,60).Select(index =>
                    comparison with { Turn = index, Selected = index < 59 ? comparison.Local : comparison.Selected }).ToArray() };
                using var compactTrace = JsonDocument.Parse(JsonSerializer.Serialize(compactLesson.Invoke(null, [repeated]),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                var attackEvidence = compactTrace.RootElement.GetProperty("observed").GetProperty("attacks");
                Check(attackEvidence.GetProperty("stormTrade").GetInt32() == 1
                    && attackEvidence.GetProperty("examples")[0].GetProperty("ownHealth").GetInt32() == 20,
                    "real attack targets and public context were not reused for future model decisions");
                var trace = compactTrace.RootElement.GetProperty("decisionComparisons");
                Check(trace.GetProperty("checkedActions").GetInt32() == 60 && trace.GetProperty("changed").GetArrayLength() == 1
                    && !trace.GetProperty("changed")[0].GetProperty("local").TryGetProperty("score",out _),
                    "unchanged decision records inflated subsequent model context");


            }
            using (var metadataProvider = new FakeProvider())
            using (var metadataPlanner = new AiStrategyPlanner(metadataProvider, config, new TestHost(), NullLogger<AiStrategyPlanner>.Instance))
            {
                var card = GameDataProvider.Instance.GetCardDefinition(2)!;
                var originalClass = card.Class;
                var originalTribe = card.Tribe;
                var readDeck = typeof(AiStrategyPlanner).GetMethod("ReadDeck", BindingFlags.Static | BindingFlags.NonPublic)!;
                var deckKey = typeof(AiStrategyPlanner).GetMethod("DeckKey", BindingFlags.Static | BindingFlags.NonPublic)!;
                object Key() => deckKey.Invoke(null, [readDeck.Invoke(null, [game, own.PlayerId])])!;
                var previousKey = Key();
                try
                {
                    metadataPlanner.WarmDeckAsync(game, own.PlayerId, enemy.Class.ToString())?.GetAwaiter().GetResult();
                    card.Class = CardClass.Forestcraft;
                    card.Tribe = ["ClassSensitive"];
                    metadataPlanner.WarmDeckAsync(game, own.PlayerId, enemy.Class.ToString())?.GetAwaiter().GetResult();
                    Check(metadataProvider.Bodies.Count == 2 && previousKey.Equals(Key()),
                        "updated class/tribe reused an obsolete guide or lost the existing experience identity");
                    Check(metadataProvider.Bodies.Last().Contains("ClassSensitive")
                        && metadataProvider.Bodies.Last().Contains("Forestcraft"),
                        "real deck metadata was not supplied for class-sensitive search planning");
                }
                finally { card.Class = originalClass; card.Tribe = originalTribe; }
                metadataPlanner.WarmDeckAsync(game, own.PlayerId, enemy.Class.ToString())?.GetAwaiter().GetResult();
                Check(metadataProvider.Bodies.Count == 2, "unchanged original rules did not reuse their matching guide");
            }
            // Provider failures must stop comparison runs, rather than silently score a local fallback.
            foreach (var code in new[] { 401, 402, 429, 500 })
            {
                using var failedProvider = new FakeProvider { ResponseStatus = (HttpStatusCode)code };
                using var guarded = new AiStrategyPlanner(failedProvider, config, new TestHost(), NullLogger<AiStrategyPlanner>.Instance);
                guarded.WarmDeckAsync(game, own.PlayerId, enemy.Class.ToString())?.GetAwaiter().GetResult();
                var availability = guarded.ProviderStatus;
                Check(!guarded.CanRequest && !availability.Available && availability.HttpStatus == code,
                    "provider HTTP failure was not exposed to evaluation status");
                Check((availability.RetryAfter == null) == (code is 401 or 402),
                    "permanent account errors and temporary provider errors shared retry behavior");
                Check(guarded.PlanAsync(game, snapshot, own.PlayerId, default, Legal).GetAwaiter().GetResult() == null,
                    "failed provider produced a new plan");
                guarded.WarmDeckAsync(game, own.PlayerId, enemy.Class.ToString())?.GetAwaiter().GetResult();
                Check(failedProvider.Bodies.Count == 1, "blocked provider was called repeatedly");
                using (var usage = JsonDocument.Parse(JsonSerializer.Serialize(guarded.ProviderUsage)))
                    Check(usage.RootElement.GetProperty("responses").GetInt64() == 0,
                        "HTTP errors were counted as successful provider responses");
                var service = new LocalAiTrainingService(null!, null!, null!, NullLogger<LocalAiTrainingService>.Instance, guarded);
                var ensure = typeof(LocalAiTrainingService).GetMethod("EnsureStrategyAvailable", BindingFlags.Instance | BindingFlags.NonPublic)!;
                ensure.Invoke(service, [new LocalTrainingRequest(Guid.NewGuid(), Guid.NewGuid())]);
                bool interrupted = false;
                try { ensure.Invoke(service, [new LocalTrainingRequest(Guid.NewGuid(), Guid.NewGuid(), CompareStrategy: true)]); }
                catch (TargetInvocationException ex) { interrupted = ex.InnerException?.Message.Contains(availability.Message) == true; }
                Check(interrupted, "LLM comparison continued after provider became unavailable");
                failedProvider.ResponseStatus = HttpStatusCode.OK;
                typeof(AiStrategyPlanner).GetMethod("RetryProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(guarded, null);
                Check(guarded.CanRequest, "explicit retry did not reset blocked provider");
                guarded.WarmDeckAsync(game, own.PlayerId, enemy.Class.ToString())?.GetAwaiter().GetResult();
                var recovered = guarded.PlanAsync(game, snapshot, own.PlayerId, default, Legal).GetAwaiter().GetResult();
                Check(recovered != null && guarded.ProviderStatus.Available && guarded.ProviderStatus.HttpStatus == null
                    && failedProvider.Bodies.Count == 3, "recovered provider did not resume valid guide and plan requests");
            }
        }
        finally
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path)) File.Delete(file);
                Directory.Delete(path);
            }
        }
    }

    private static void Check(bool passed, string message)
    {
        if (!passed) throw new InvalidOperationException(message);
    }

    private sealed class TestHost : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = ".";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FakeProvider : HttpMessageHandler, IHttpClientFactory
    {
        public List<string> Bodies { get; } = new();
        public bool InvalidReflection;
        public HttpStatusCode ResponseStatus = HttpStatusCode.OK;
        public List<string> Prefixes { get; } = new();
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var messages = payload.RootElement.GetProperty("messages");
            Prefixes.Add(string.Join("\n", messages.EnumerateArray().Take(2).Select(message => message.GetRawText())));
            var fields = new Dictionary<string, JsonElement>();
            foreach (var message in messages.EnumerateArray().Where(message => message.GetProperty("role").GetString() == "user"))
            {
                using var content = JsonDocument.Parse(message.GetProperty("content").GetString()!);
                foreach (var field in content.RootElement.EnumerateObject()) fields[field.Name] = field.Value.Clone();
            }
            var body = JsonSerializer.Serialize(fields);
            Bodies.Add(body);
            if (ResponseStatus != HttpStatusCode.OK)
                return new HttpResponseMessage(ResponseStatus) { Content = new StringContent("provider unavailable") };
            using var data = JsonDocument.Parse(body);
            if (data.RootElement.TryGetProperty("options", out var candidates)
                && candidates[0].GetProperty("name").GetString() == "Timeout")
                throw new OperationCanceledException();
            var answer = data.RootElement.TryGetProperty("games", out _)
                ? (InvalidReflection ? """[{"hypothesis":"unsupported","sources":[99]}]""" : """[{"hypothesis":"test resource conservation #2","sources":[0,1]}]""")
                : data.RootElement.TryGetProperty("deck", out _) && !data.RootElement.TryGetProperty("own", out _) && !data.RootElement.TryGetProperty("options", out _)
                ? """{"gamePlan":"plan","cards":[{"id":1,"role":"tempo"},{"id":2,"role":"payoff"}]}"""
                : data.RootElement.TryGetProperty("options", out var options)
                    ? (options[0].GetProperty("name").GetString() == "Invalid" ? """{"indexes":[9]}""" : """{"indexes":[0]}""")
                    : """{"focus":"pressure","plan":"play #2","prioritize":[2,1,999],"hold":[2,999],"ability":{"cardId":2,"kind":"act","cost":1,"name":"Draw","intent":"use"}}""";
            var envelope = JsonSerializer.Serialize(new { usage = new { prompt_tokens = 1000, prompt_cache_hit_tokens = 600, completion_tokens = 50 }, choices = new[] { new { message = new { content = answer } } } });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(envelope) };
        }
    }
}
