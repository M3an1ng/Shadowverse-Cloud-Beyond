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
            HandActions = [new(3, 2, "play"), new(3, 1, "discard")],
            PlanValidations=[new(3,[new(0,2,"Base",2,"use")],"NOT_EXECUTED_CONSTRAINT_CHECK",false)]
        };
        var evidenceMethod = typeof(AiStrategyPlanner).GetMethod("EvidenceForModel", BindingFlags.Static | BindingFlags.NonPublic)!;
        var evidenceJson = JsonSerializer.Serialize(evidenceMethod.Invoke(null, [proposedOnly]),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Check(!evidenceJson.Contains("UNEXECUTED_PLAN") && !evidenceJson.Contains("NOT_EXECUTED_CONSTRAINT_CHECK") && evidenceJson.Contains("\"kind\":\"discard\"")
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
            ["AiStrategy:GuideCacheEnabled"] = "false",
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
                List<Guid>? Select(PlayerChoiceRequest request, Guid[]? local = null) =>
                    ((Task<List<Guid>?>)chooseMode.Invoke(planner, [game, snapshot, request, source, local, CancellationToken.None])!)
                        .GetAwaiter().GetResult();
                PlayerChoiceRequest MakeChoice(string name)
                {
                    var first = Guid.NewGuid();
                    var second = Guid.NewGuid();
                    var result=new PlayerChoiceRequest
                    {
                        RequestingPlayerId = own.PlayerId,
                        TargetKey = "decision",
                        MinSelectionCount = 1,
                        MaxSelectionCount = 1,
                        SelectableTargetIds = [first, second],
                        SelectableCardInstances = [new() { InstanceId = first, Name = name }, new() { InstanceId = second, Name = "Option 2" }]
                    };
                    typeof(PlayerChoiceRequest).GetProperty("DecisionOptions",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(result,
                        new Dictionary<Guid,AbilityConfig>{{first,new(){Name=name}},{second,new(){Name="Option 2"}}});
                    return result;
                }
                var fullText = new string('文', 1700) + "TAIL_RULE_完整条件";
                var longChoice = MakeChoice(new string('模', 130) + "TAIL_NAME_关键分支");
                longChoice.SelectableCardInstances[0] = longChoice.SelectableCardInstances[0] with { Description = fullText };
                Check(Select(longChoice) != null && provider.Bodies.Last().Contains("TAIL_RULE_")
                    && provider.Bodies.Last().Contains("TAIL_NAME_"), "mode or card text was silently clipped before model selection");
                var choice = MakeChoice("Option 1");
                Check(Select(choice, [choice.SelectableTargetIds[1]])?.SequenceEqual([choice.SelectableTargetIds[0]]) == true
                    && provider.Bodies.Last().Contains("\"localCandidateIndexes\":[1]")
                    && !provider.Bodies.Last().Contains(choice.SelectableTargetIds[1].ToString()),
                    "local mode alternative was omitted or exposed its runtime ID");
                Check(provider.Prefixes.Take(4).Distinct().Count() == 1,
                    "guide, turn and mode requests did not share a stable rules prefix");
                var sameChoice = MakeChoice("Option 1");
                Check(Select(sameChoice, [sameChoice.SelectableTargetIds[1]])?.SequenceEqual([sameChoice.SelectableTargetIds[0]]) == true
                    && provider.Bodies.Count == 4, "equivalent repeat did not remap cached index to current GUID");
                Check(Select(MakeChoice("Invalid")) == null, "illegal model index was accepted");
                var discover = MakeChoice("Option 1");
                discover.TargetKey = "discover";
                discover.SelectableCardInstances[0] = discover.SelectableCardInstances[0] with { CardId = 2, CurrentCost = 2 };
                var illegalLocal = Guid.NewGuid();
                Check(Select(discover, [illegalLocal])?.SequenceEqual([discover.SelectableTargetIds[0]]) == true
                    && provider.Bodies.Last().Contains("\"currentCost\":2")
                    && provider.Bodies.Last().Contains("\"localCandidateIndexes\":null")
                    && !provider.Bodies.Last().Contains(illegalLocal.ToString()), "discover omitted card data or leaked an illegal alternative");
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
                provider.BeforeOptionResponse = () => typeof(GameState).GetMethod("IncrementStateRevision",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, null);
                Check(Select(MakeChoice("Stale")) == null, "a response from an old revision was applied");
                provider.BeforeOptionResponse = null;
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
                var selected = ((Task<List<Guid>?>)method.Invoke(choicesOnly, [next, snapshot, request, null, new[] { other }, CancellationToken.None])!)
                    .GetAwaiter().GetResult();
                Check(selected?.SequenceEqual([option]) == true && provider.Bodies.Count == callsBefore + 1
                    && provider.Bodies.Last().Contains("\"matchupGames\":1")
                    && provider.Bodies.Last().Contains("\"localCandidateIndexes\":[1]"),
                    "choices-only strategy omitted a legal choice, local alternative or prior experience");
            }
            using (var reflectionPlanner = new AiStrategyPlanner(provider, config, new TestHost(), NullLogger<AiStrategyPlanner>.Instance))
            {
                var queue = typeof(AiStrategyPlanner).GetMethod("QueueReflection", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var running = typeof(AiStrategyPlanner).GetField("guideRunning", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var rulesType = typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiDefinitionRules")!;
                var stamp = (AiDefinitionStamp)rulesType.GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, [GameDataProvider.Instance])!;
                var evidence = Enumerable.Range(0, 4).Select(i => (Lesson(i % 2 == 0, i < 2) with { Played = [2], DefinitionRules = stamp, Plans = ["UNVERIFIED_REVIEW_"+i] })).ToArray();
                var key = new string('a', 64);
                queue.Invoke(reflectionPlanner, [key, evidence]);
                Check(SpinWait.SpinUntil(() => !(bool)running.GetValue(reflectionPlanner)!, TimeSpan.FromSeconds(5)),
                    "background reflection did not finish");
                using(var reflectionRequest=JsonDocument.Parse(provider.Bodies.Last())) {
                    var rows=reflectionRequest.RootElement.GetProperty("games");
                    Check(rows.GetArrayLength()==4,"reflection request lost source matches");
                    for(var index=0;index<4;index++) {
                        var row=rows[index];var review=row.GetProperty("planTextReview");
                        Check(review.GetProperty("texts")[0].GetProperty("value").GetString()==evidence[index].Plans[0]
                            && !row.GetProperty("facts").GetRawText().Contains("UNVERIFIED_REVIEW_"),
                            "reflection lost archived explanations or promoted them into committed evidence");
                    }
                }
                var reflectionPath = Path.Combine(path, key + ".reflection.json");
                var saved = JsonSerializer.Deserialize<AiStrategyPlanner.StrategyReflection>(File.ReadAllText(reflectionPath),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                Check(saved.Insights[0].Sources.SequenceEqual([0, 1]) && saved.Insights[0].ObservedCardIds?.SequenceEqual([2]) == true
                    && saved.Evidence.Length == 4,
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

                var resources = new AiObservedResources(20, 20, 2, 2, 0, 0, false, false, false, 0,
                    2, 2, 0, 0, 0, 0, "own", false, false);
                var completedAction = new AiCompletedActionFact(2, new("PlayCard", 2, null, null, 0), resources, resources,
                    null, null, null, null, "completed", Decision: new("model-first-action", new("PlayCard", 1, null, null, 0), true));
                var groundedEvidence = new[] {
                    evidence[0] with { Played = [], CompletedActions = [completedAction] },
                    evidence[1], evidence[2] with { Played = [1] }, evidence[3]
                };
                provider.ReflectionAnswer = """
                    [{"hypothesis":"unsupported unplayed local alternative #1","sources":[0]},
                     {"hypothesis":"unsupported unrelated source #2","sources":[0,2]},
                     {"hypothesis":"grounded observed action #2","sources":[0,1],"observedCardIds":[1]}]
                    """;
                var groundedKey = new string('c', 64);
                Check((bool)queue.Invoke(reflectionPlanner, [groundedKey, groundedEvidence])!, "grounded reflection did not queue");
                Check(SpinWait.SpinUntil(() => !(bool)running.GetValue(reflectionPlanner)!, TimeSpan.FromSeconds(5)), "grounded reflection did not finish");
                using (var request = JsonDocument.Parse(provider.Bodies.Last()))
                {
                    var games = request.RootElement.GetProperty("games");
                    Check(games[0].GetProperty("observedCardIds").EnumerateArray().Select(id => id.GetInt32()).SequenceEqual([2])
                        && games[2].GetProperty("observedCardIds")[0].GetInt32() == 1,
                        "grounding omitted a completed action or treated the unplayed local alternative as observed");
                }
                var grounded = (AiStrategyPlanner.StrategyReflection)typeof(AiStrategyPlanner)
                    .GetMethod("ReadReflection", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(reflectionPlanner, [groundedKey])!;
                var insight = grounded.Insights.Single();
                Check(insight.Hypothesis.StartsWith("grounded observed action", StringComparison.Ordinal)
                    && insight.Sources.SequenceEqual([0, 1]) && insight.ObservedCardIds?.SequenceEqual([2]) == true,
                    "invalid cross-game or unsupported suggestions were persisted, or supplied IDs were trusted");
                var forNextPlan = typeof(AiStrategyPlanner).GetMethod("SummarizeExperience", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(reflectionPlanner, [groundedEvidence, "Havencraft", groundedKey]);
                using (var nextPlan = JsonDocument.Parse(JsonSerializer.Serialize(forNextPlan, new JsonSerializerOptions(JsonSerializerDefaults.Web))))
                {
                    var insights = nextPlan.RootElement.GetProperty("hypotheses").GetProperty("insights");
                    Check(insights.GetArrayLength() == 1 && insights[0].GetProperty("observedCardIds")[0].GetInt32() == 2
                        && !nextPlan.RootElement.GetRawText().Contains("unsupported unrelated source"),
                        "a later planning input lost grounding or reused an unsupported reflection");
                }
                VerifyDrawReflection(reflectionPlanner,provider,path,evidence,config,own,enemy);
                provider.ReflectionAnswer = null;
                provider.InvalidReflection = false;
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
            // A restart with the same rules reuses the guide regardless of age; rule/model changes and invalid data miss.
            var cachePath=Path.Combine(path,"cache-tests");
            var cacheConfig=new ConfigurationBuilder().AddConfiguration(config).AddInMemoryCollection(new Dictionary<string,string?> {
                ["AiStrategy:GuideCacheEnabled"]="true",["AiStrategy:MemoryPath"]=cachePath
            }).Build();
            using(var cacheProvider=new FakeProvider()) {
                AiStrategyPlanner.DeckGuide? Warm(IConfiguration cfg) {
                    using var instance=new AiStrategyPlanner(cacheProvider,cfg,new TestHost(),NullLogger<AiStrategyPlanner>.Instance);
                    return instance.WarmDeckAsync(game,own.PlayerId,enemy.Class.ToString())?.GetAwaiter().GetResult();
                }
                Check(Warm(cacheConfig)!=null&&cacheProvider.Bodies.Count==1,"initial guide was not generated");
                Check(Warm(cacheConfig)!=null&&cacheProvider.Bodies.Count==1,"restart paid again for an identical valid guide");
                var cacheFile=Directory.GetFiles(Path.Combine(cachePath,"guides"),"*.json").Single();
                var original=File.ReadAllText(cacheFile);
                var saved=System.Text.Json.Nodes.JsonNode.Parse(original)!;
                saved["createdAt"]=DateTimeOffset.UtcNow.AddHours(-13).ToString("O");File.WriteAllText(cacheFile,saved.ToJsonString());
                Check(Warm(cacheConfig)!=null&&cacheProvider.Bodies.Count==1,"unchanged rules paid again after thirteen hours");
                saved=System.Text.Json.Nodes.JsonNode.Parse(original)!;saved["guide"]!["cards"]=new System.Text.Json.Nodes.JsonArray();
                File.WriteAllText(cacheFile,saved.ToJsonString());
                Check(Warm(cacheConfig)!=null&&cacheProvider.Bodies.Count==2,"incomplete guide was reused");
                File.WriteAllText(cacheFile,"invalid json");
                Check(Warm(cacheConfig)!=null&&cacheProvider.Bodies.Count==3,"corrupt guide prevented regeneration");
                var otherModel=new ConfigurationBuilder().AddConfiguration(cacheConfig).AddInMemoryCollection(new Dictionary<string,string?> {
                    ["AiStrategy:Model"]="other-model"
                }).Build();
                Check(Warm(otherModel)!=null&&cacheProvider.Bodies.Count==4,"another model reused the wrong cached guide");
                var definition=GameDataProvider.Instance.GetCardDefinition(2)!;var oldClass=definition.Class;
                try {
                    definition.Class=CardClass.Forestcraft;
                    Check(Warm(cacheConfig)!=null&&cacheProvider.Bodies.Count==5,"changed rules reused a stale disk guide");
                } finally {definition.Class=oldClass;}
                Check(!original.Contains("apiKey")&&!original.Contains("Authorization"),"guide persisted credentials");
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
            var resolved=Path.GetFullPath(path);
            if(!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)
                ||!Path.GetFileName(resolved).StartsWith("sv-ai-strategy-",StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected test cleanup directory");
            if(Directory.Exists(resolved))Directory.Delete(resolved,true);
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

    private static void CleanupFixture(string path, string prefix)
    {
        var resolved = Path.GetFullPath(path);
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(resolved).StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected offline fixture directory");
        if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
    }

    private static AiDefinitionStamp CurrentRules()
    {
        var type = typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiDefinitionRules")!;
        return (AiDefinitionStamp)type.GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [GameDataProvider.Instance])!;
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan elapsed) => now += elapsed;
    }

    public static void GuideReuseAndCooldown()
    {
        var path = Path.Combine(Path.GetTempPath(), "sv-guide-reuse-" + Guid.NewGuid().ToString("N"));
        var clock = new TestClock();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiStrategy:Enabled"] = "true", ["AiStrategy:GuideCacheEnabled"] = "true",
            ["AiStrategy:Endpoint"] = "https://example.invalid/chat/completions",
            ["AiStrategy:Model"] = "mock", ["AiStrategy:MemoryPath"] = path
        }).Build();
        var own = new PlayerState(Guid.NewGuid()) { IsAI = true, InitialDeckDefinitionIds = [1, 2, 2] };
        var enemy = new PlayerState(Guid.NewGuid());
        var game = new GameState("guide-reuse-only", own, enemy, own.PlayerId);
        using var http = new FakeProvider { InvalidGuide = true };
        AiStrategyPlanner New(IConfiguration? cfg = null) => new(http, cfg ?? config, new TestHost(),
            NullLogger<AiStrategyPlanner>.Instance, clock: clock);
        Task<AiStrategyPlanner.DeckGuide?> Warm(AiStrategyPlanner planner) =>
            planner.WarmDeckAsync(game, own.PlayerId, enemy.Class.ToString())!;
        AiStrategyPlanner.DeckGuide? Restart()
        {
            using var fresh = New();
            return Warm(fresh).GetAwaiter().GetResult();
        }
        try
        {
            using (var planner = New())
            {
                Check(Warm(planner).GetAwaiter().GetResult() == null && http.Bodies.Count == 1, "invalid guide did not fail closed");
                clock.Advance(TimeSpan.FromMinutes(9));
                for (var i = 0; i < 10; i++) Check(Warm(planner).GetAwaiter().GetResult() == null, "failed guide became valid");
                Check(http.Bodies.Count == 1, "repeated turns retried an invalid paid analysis before cooldown");
                clock.Advance(TimeSpan.FromMinutes(1));
                http.InvalidGuide = false;
                Check(Warm(planner).GetAwaiter().GetResult() != null && http.Bodies.Count == 2, "guide was not retried at cooldown boundary");
                clock.Advance(TimeSpan.FromDays(180));
                Check(Warm(planner).GetAwaiter().GetResult() != null && http.Bodies.Count == 2, "unchanged in-memory rules paid again after elapsed time");
            }
            Check(Restart() != null && http.Bodies.Count == 2, "old but rule-identical disk guide paid again after restart");
            var file = Directory.GetFiles(Path.Combine(path, "guides"), "*.json").Single();
            var saved = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!;
            saved["createdAt"] = clock.GetUtcNow().AddMinutes(6).ToString("O");
            File.WriteAllText(file, saved.ToJsonString());
            Check(Restart() != null && http.Bodies.Count == 3, "future-dated cache bypassed validation");
            saved["createdAt"] = DateTimeOffset.MinValue.ToString("O");
            File.WriteAllText(file, saved.ToJsonString());
            Check(Restart() != null && http.Bodies.Count == 4, "unversioned cache timestamp bypassed validation");

            var uncached = new ConfigurationBuilder().AddConfiguration(config)
                .AddInMemoryCollection(new Dictionary<string, string?> { ["AiStrategy:GuideCacheEnabled"] = "false" }).Build();
            using var retry = New(uncached);
            http.InvalidGuide = true;
            Check(Warm(retry).GetAwaiter().GetResult() == null && http.Bodies.Count == 5, "retry fixture failed");
            typeof(AiStrategyPlanner).GetMethod("RetryProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(retry, null);
            http.InvalidGuide = false;
            Check(Warm(retry).GetAwaiter().GetResult() != null && http.Bodies.Count == 6, "explicit retry did not release failed-guide cooldown");
            typeof(AiStrategyPlanner).GetMethod("RetryProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(retry, null);
            Check(Warm(retry).GetAwaiter().GetResult() != null && http.Bodies.Count == 6, "explicit retry discarded a successful guide");

            using var coalesced = New(uncached);
            http.GuideResponseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = Warm(coalesced);
            Check(SpinWait.SpinUntil(() => http.Bodies.Count == 7, TimeSpan.FromSeconds(5)), "analysis did not start");
            var again = Warm(coalesced);
            Check(ReferenceEquals(first, again), "in-flight guide was not shared");
            http.GuideResponseGate.SetResult(true);
            Check(first.GetAwaiter().GetResult() != null && again.GetAwaiter().GetResult() != null && http.Bodies.Count == 7,
                "concurrent warming duplicated the same guide request");
            Console.WriteLine("PASS guide reuse: 180-day restart hit; 10 failed warmings suppressed; explicit retry and concurrent coalescing verified; real provider calls=0");
        }
        finally { http.GuideResponseGate?.TrySetResult(true); CleanupFixture(path, "sv-guide-reuse-"); }
    }

    public static void FrozenBenchmarkExperience()
    {
        var path = Path.Combine(Path.GetTempPath(), "sv-frozen-benchmark-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiStrategy:Enabled"] = "true", ["AiStrategy:Endpoint"] = "https://example.invalid/chat/completions",
            ["AiStrategy:Model"] = "mock", ["AiStrategy:MemoryPath"] = path,
            ["AiStrategy:GuideCacheEnabled"] = "false"
        }).Build();
        using var http = new FakeProvider();
        using var planner = new AiStrategyPlanner(http, config, new TestHost(), NullLogger<AiStrategyPlanner>.Instance);
        const BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var type = typeof(AiStrategyPlanner);
        string Key(int[] cards) => (string)type.GetMethod("DeckKey", hidden)!.Invoke(null,
            [type.GetMethod("ReadDeckIds", hidden)!.Invoke(null, [cards])])!;
        var first = new DeckDetailDto { DeckId = Guid.NewGuid(), Cards = [new() { CardDefinitionId = 1, Count = 1 }, new() { CardDefinitionId = 2, Count = 1 }] };
        var second = first with { DeckId = Guid.NewGuid(), Cards = [first.Cards[0], first.Cards[1] with { Count = 2 }] };
        var firstKey = Key([1, 2]);
        var secondKey = Key([1, 2, 2]);
        var stamp = CurrentRules();
        var now = DateTimeOffset.UtcNow;
        AiStrategyPlanner.Lesson Example(int offset) => new("Neutral", true, false, 6, "fixture", [], [2],
            now.AddMinutes(-offset), DefinitionRules: stamp);
        var original = new[] { Example(3), Example(2), Example(1) };
        void Write<T>(string file, T value) => File.WriteAllText(Path.Combine(path, file + ".json"), JsonSerializer.Serialize(value, json));
        AiStrategyPlanner.StrategyReflection Reflection(string label) => new(now, [new(label, [0, 1])], original, 3);
        try
        {
            Write(firstKey, original);
            Write(secondKey, new[] { Example(1) });
            Write(firstKey + ".reflection", Reflection("FROZEN_REFLECTION"));
            var prepare = (Task)type.GetMethod("PrepareBenchmarkAsync", hidden)!.Invoke(planner, [first, second, CancellationToken.None, false])!;
            prepare.GetAwaiter().GetResult();
            var frozen = prepare.GetType().GetProperty("Result")!.GetValue(prepare)!;
            Check(http.Bodies.Count == 2, "benchmark did not pin a guide for each distinct deck");
            (GameState Game, GameStateDto View, PlayerState Own, PlayerState Enemy) Position(int[] cards, bool benchmark)
            {
                var own = new PlayerState(Guid.NewGuid()) { IsAI = true, TurnNumber = 6, InitialDeckDefinitionIds = cards.ToList() };
                var enemy = new PlayerState(Guid.NewGuid()) { TurnNumber = 6 };
                var game = new GameState(Guid.NewGuid().ToString(), own, enemy, own.PlayerId)
                    { IsLocalAiTraining = benchmark, Phase = GamePhase.Main, Status = GameStatus.InProgress };
                if (benchmark)
                {
                    typeof(GameState).GetProperty("StrategyBenchmarkPlayerId", hidden)!.SetValue(game, own.PlayerId);
                    type.GetMethod("BindBenchmarkGame", hidden)!.Invoke(planner, [game, own.PlayerId, frozen, null]);
                }
                var view = new GameStateDto { GameId = game.GameId, CurrentPlayerId = own.PlayerId, Phase = GamePhase.Main,
                    Players = [new() { PlayerId = own.PlayerId, TurnNumber = 6 }, new() { PlayerId = enemy.PlayerId, TurnNumber = 6 }] };
                return (game, view, own, enemy);
            }
            void Plan((GameState Game, GameStateDto View, PlayerState Own, PlayerState Enemy) position) =>
                Check(planner.PlanAsync(position.Game, position.View, position.Own.PlayerId, default,
                    (_, _) => Task.FromResult(new PlayabilityResultDto())).GetAwaiter().GetResult() != null, "turn planning failed");
            void Input(int games, string? reflection)
            {
                using var body = JsonDocument.Parse(http.Bodies.Last());
                var experience = body.RootElement.GetProperty("experience");
                Check(experience.GetProperty("allGames").GetInt32() == games, "model got a changing or wrong deck history");
                var hypotheses = experience.GetProperty("hypotheses");
                Check(reflection == null ? hypotheses.ValueKind == JsonValueKind.Null
                    : hypotheses.GetProperty("insights")[0].GetProperty("hypothesis").GetString() == reflection,
                    "model got a changing or wrong deck reflection");
                Check(body.RootElement.GetProperty("guide").GetProperty("gamePlan").GetString() == "plan", "pinned guide not sent");
            }
            var early = Position([1, 2], true);
            Plan(early);
            Input(3, "FROZEN_REFLECTION");
            planner.ObserveCommittedEvents(early.Game, [new GameEndEvent(early.Own.PlayerId, early.Enemy.PlayerId, "benchmark fixture")]);
            var history = JsonSerializer.Deserialize<AiStrategyPlanner.Lesson[]>(File.ReadAllText(Path.Combine(path, firstKey + ".json")), json)!;
            Check(history.Length == 4 && history[^1].Mode == "benchmark", "benchmark stopped saving results for future learning");
            Check(!(bool)type.GetField("guideRunning", hidden)!.GetValue(planner)!
                && !http.Bodies.Any(body => body.Contains("\"games\":")), "benchmark queued an extra reflection request");

            Write(firstKey + ".reflection", Reflection("NEW_REFLECTION"));
            Write(secondKey, new[] { Example(4), Example(3), Example(2), Example(1) });
            var later = Position([2, 1], true);
            Plan(later);
            Input(3, "FROZEN_REFLECTION");
            var otherDeck = Position([2, 1, 2], true);
            Plan(otherDeck);
            Input(1, null);

            var optionIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var request = new PlayerChoiceRequest { RequestingPlayerId = later.Own.PlayerId, TargetKey = "decision",
                MinSelectionCount = 1, MaxSelectionCount = 1, SelectableTargetIds = optionIds.ToList(),
                SelectableCardInstances = [new() { InstanceId = optionIds[0], Name = "Option 1" }, new() { InstanceId = optionIds[1], Name = "Option 2" }] };
            typeof(PlayerChoiceRequest).GetProperty("DecisionOptions", hidden)!.SetValue(request,
                new Dictionary<Guid, AbilityConfig> { [optionIds[0]] = new() { Name = "Option 1" }, [optionIds[1]] = new() { Name = "Option 2" } });
            var choice = (Task<List<Guid>?>)type.GetMethod("ChooseFromOptionsAsync", hidden)!.Invoke(planner,
                [later.Game, later.View, request, null, null, CancellationToken.None])!;
            Check(choice.GetAwaiter().GetResult()?.SequenceEqual([optionIds[0]]) == true, "benchmark choice failed");
            Input(3, "FROZEN_REFLECTION");

            Plan(Position([1, 2], false));
            Input(4, "NEW_REFLECTION");
            Check(http.Bodies.Count == 7, "benchmark guide refresh, reflection, or missing model request changed the fixture call count");
            Console.WriteLine("PASS benchmark learning inputs: two pinned guides, frozen per-deck lessons/reflection for turns and choices, persisted completion, live match sees new data; scripted requests only");
        }
        finally { CleanupFixture(path, "sv-frozen-benchmark-"); }
    }

    public static void CompatibleExperience()
    {
        var path = Path.Combine(Path.GetTempPath(), "sv-compatible-lessons-" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiStrategy:Enabled"] = "true", ["AiStrategy:Endpoint"] = "https://example.invalid/chat/completions",
            ["AiStrategy:Model"] = "mock", ["AiStrategy:MemoryPath"] = path
        }).Build();
        using var http = new FakeProvider();
        using var planner = new AiStrategyPlanner(http, config, new TestHost(), NullLogger<AiStrategyPlanner>.Instance);
        var stamp = CurrentRules();
        Check(stamp.Available, "experience fixture rules unavailable");
        var at = DateTimeOffset.UtcNow;
        AiStrategyPlanner.Lesson Lesson(bool won, bool first, string opponent = "Havencraft") =>
            new(opponent, first, won, 8, "leader defeat", [], [2], at, Choices: [new(3, 2, "mode", [1], Position: new(20, 20, 2, 2, 0, 0, 0, 0, 0) { NativeOptionRules = new string('a', 64) })], DefinitionRules: stamp);
        var relevant = new[] { Lesson(true, true), Lesson(false, false) };
        var foreign = new[]
        {
            Lesson(true, true) with { DefinitionRules = stamp with { Fingerprint = "old-config" } },
            Lesson(true, true) with { DefinitionRules = stamp with { EngineFingerprint = "old-engine" } },
            Lesson(true, true) with { DefinitionRulesChanged = true },
            Lesson(true, true) with { DefinitionRules = null }
        }.SelectMany(row => Enumerable.Range(0, 4).Select(i => row with { FinishedAt = at.AddSeconds(i + 1) })).ToArray();
        var all = relevant.Append(Lesson(true, true, "Forestcraft")).Concat(foreign).ToArray();
        var key = new string('c', 64);
        try
        {
            var choose = typeof(AiStrategyPlanner).GetMethod("SelectModelLessons", BindingFlags.Static | BindingFlags.NonPublic)!;
            var selected = (AiStrategyPlanner.Lesson[])choose.Invoke(null, [all, "Havencraft"])!;
            Check(selected.Length == 8 && selected.Take(3).All(lesson => lesson.DefinitionRules?.Fingerprint == stamp.Fingerprint
                && lesson.DefinitionRules.EngineFingerprint == stamp.EngineFingerprint && !lesson.DefinitionRulesChanged)
                && selected.Skip(3).Any(lesson => lesson.DefinitionRules == null || lesson.DefinitionRules.Fingerprint != stamp.Fingerprint
                    || lesson.DefinitionRules.EngineFingerprint != stamp.EngineFingerprint || lesson.DefinitionRulesChanged),
                "recent obsolete games crowded out current facts, or old transferable clues were deleted");
            var method = typeof(AiStrategyPlanner).GetMethod("SummarizeExperience", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var value = JsonSerializer.SerializeToElement(method.Invoke(planner, [all, "Havencraft", key]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Check(value.GetProperty("allGames").GetInt32() == 19 && value.GetProperty("compatibleGames").GetInt32() == 3
                && value.GetProperty("excludedFromStats").GetInt32() == 16, "history was discarded or obsolete outcomes were counted as current");
            Check(value.GetProperty("matchupGames").GetInt32() == 2 && value.GetProperty("matchupWins").GetInt32() == 1
                && value.GetProperty("firstGames").GetInt32() == 1 && value.GetProperty("secondGames").GetInt32() == 1,
                "old or other-matchup victories inflated current matchup statistics");
            var mode = value.GetProperty("modes").EnumerateArray().Single();
            Check(mode.GetProperty("games").GetInt32() == 2 && mode.GetProperty("wins").GetInt32() == 1,
                "mode advice used victories from incompatible rules");
            var counts = value.GetProperty("ruleCompatibility");
            Check(counts.GetProperty("config-changed").GetInt32() == 4 && counts.GetProperty("engine-changed").GetInt32() == 4
                && counts.GetProperty("mixed-observed-configs").GetInt32() == 4 && counts.GetProperty("unknown").GetInt32() == 4,
                "older transferable observations lost their scope labels");
            var queue = typeof(AiStrategyPlanner).GetMethod("QueueReflection", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Check(!(bool)queue.Invoke(planner, [key, all])! && http.Bodies.Count == 0,
                "old games triggered a paid reflection without four current-rule examples");
            Directory.CreateDirectory(path);
            // A previous reflection under old rules must not block four valid current-rule examples.
            var stale = new AiStrategyPlanner.StrategyReflection(at.AddHours(1), [new("old #2", [0])], foreign[..4], 3);
            var file = Path.Combine(path, key + ".reflection.json");
            File.WriteAllText(file, JsonSerializer.Serialize(stale, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            var four = all.Append(Lesson(true, false)).ToArray();
            Check((bool)queue.Invoke(planner, [key, four])!, "old reflection timestamp blocked new-rule learning");
            var running = typeof(AiStrategyPlanner).GetField("guideRunning", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Check(SpinWait.SpinUntil(() => !(bool)running.GetValue(planner)!, TimeSpan.FromSeconds(5)), "compatible reflection did not finish");
            var reflection = JsonSerializer.Deserialize<AiStrategyPlanner.StrategyReflection>(File.ReadAllText(file), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Check(http.Bodies.Count == 1 && reflection.Evidence.Length == 4
                && reflection.Evidence.All(lesson => lesson.DefinitionRules?.Fingerprint == stamp.Fingerprint
                    && lesson.DefinitionRules.EngineFingerprint == stamp.EngineFingerprint && !lesson.DefinitionRulesChanged),
                "reflection mixed old and current rules or lost current evidence");
            using var body = JsonDocument.Parse(http.Bodies.Single());
            Check(body.RootElement.GetProperty("games").GetArrayLength() == 4, "model received incompatible reflection facts");
            Check(!(bool)queue.Invoke(planner, [key, four])! && http.Bodies.Count == 1,
                "reflection repeated without four additional compatible games");
            Console.WriteLine("PASS experience compatibility: retained 19 lessons, counted 3 compatible, 2 matchup; obsolete-only reflection blocked; current evidence reopens learning; real provider calls=0");
        }
        finally { CleanupFixture(path, "sv-compatible-lessons-"); }
    }

    private static void VerifyDrawReflection(AiStrategyPlanner planner,FakeProvider provider,string path,
        AiStrategyPlanner.Lesson[] baseEvidence,IConfiguration config,PlayerState own,PlayerState enemy)
    {
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        var queue=typeof(AiStrategyPlanner).GetMethod("QueueReflection",hidden)!;
        var running=typeof(AiStrategyPlanner).GetField("guideRunning",hidden)!;
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var before=new AiObservedResources(12,6,0,7,0,0,true,true,false,0,0,2,2,2,8,8,"own",false,false);
        var loss=new AiCompletedActionFact(7,new("EndTurn",null,null,null,0),before,before with {OwnHand=4},
            null,null,null,null,"lost")
        {
            EndTurnDraws=new([new(2,4,4),new(1,1,0)],true,false){OwnDeckBefore=2,ExhaustedBatchIndex=1}
        };
        var gain=loss with
        {
            After=before with {OwnHand=5},Result="not-ended",
            EndTurnDraws=new([new(2,4,4),new(1,1,1)],false,false){OwnDeckBefore=3}
        };
        var other=loss with {Action=new("PerformAttack",null,null,null,0),EndTurnDraws=null,Result="not-ended"};
        var evidence=new[]
        {
            baseEvidence[0] with {Won=false,EndReason="牌堆抽空",CompletedActions=[other,other,gain,other,loss,other]},
            baseEvidence[1] with {CompletedActions=[gain]},baseEvidence[2],baseEvidence[3]
        };
        AiStrategyPlanner.StrategyReflection Save(string key,string answer)
        {
            provider.ReflectionAnswer=answer;
            Check((bool)queue.Invoke(planner,[key,evidence])!,"draw reflection did not queue");
            Check(SpinWait.SpinUntil(()=>!(bool)running.GetValue(planner)!,TimeSpan.FromSeconds(5)),"draw reflection did not finish");
            return JsonSerializer.Deserialize<AiStrategyPlanner.StrategyReflection>(
                File.ReadAllText(Path.Combine(path,key+".reflection.json")),json)!;
        }

        var lossKey=new string('d',64);
        var grounded=Save(lossKey,"""
            [{"hypothesis":"错误合并 #2 #1","sources":[0],"drawCitations":[{"gameIndex":0,"actionIndex":4,"observation":"recorded-batches-completed"}]},
             {"hypothesis":"错误抽空 #1","sources":[1],"drawCitations":[{"gameIndex":1,"actionIndex":0,"observation":"deck-exhausted"}]},
             {"hypothesis":"留牌避开 #1 后续抽空","sources":[0],"drawCitations":[{"gameIndex":0,"actionIndex":4,"observation":"deck-exhausted"}],
              "drawObservations":[{"gameIndex":0,"actionIndex":4,"ownDeckBefore":999,"result":"won"}]}]
            """);
        var proof=grounded.Insights.Single().DrawObservations!.Single();
        Check(proof is {GameIndex:0,ActionIndex:4,Result:"lost",OwnDeckBefore:2,HandBefore:0,HandAfter:4,DeckExhausted:true,ExhaustedBatchIndex:1}
            && proof.Batches.Select(batch=>batch.Resolved).SequenceEqual([4,0]),"verified terminal batch was omitted, invented, or altered");
        using (var prompt=JsonDocument.Parse(provider.Bodies.Last()))
            Check(prompt.RootElement.GetProperty("games")[0].GetProperty("facts").GetProperty("completedActions").GetProperty("samples")
                .EnumerateArray().Select(row=>row.GetProperty("sourceIndex").GetInt32()).SequenceEqual([0,1,4,5]),
                "reflection was allowed to cite a hidden retained row");

        var otherEvidence=Save(new string('e',64),"""
            [{"hypothesis":"未展示的旧动作 #1","sources":[0],"drawCitations":[{"gameIndex":0,"actionIndex":2,"observation":"recorded-batches-completed"}]},
             {"hypothesis":"#2 首批抽满","sources":[0],"drawCitations":[{"gameIndex":0,"actionIndex":4,"observation":"first-batch-completed"}]},
             {"hypothesis":"#1 第二批抽满","sources":[1],"drawCitations":[{"gameIndex":1,"actionIndex":0,"observation":"recorded-batches-completed"}]}]
            """);
        Check(otherEvidence.Insights.Length==2 && otherEvidence.Insights[0].DrawObservations![0].DeckExhausted
            && !otherEvidence.Insights[1].DrawObservations![0].DeckExhausted,
            "first-batch success was confused with the full outcome or an unshown action was accepted");

        var planMemory=Path.Combine(path,"verified-draw-planning");
        Directory.CreateDirectory(planMemory);
        var planConfig=new ConfigurationBuilder().AddConfiguration(config)
            .AddInMemoryCollection(new Dictionary<string,string?>{["AiStrategy:MemoryPath"]=planMemory}).Build();
        var testOwner=new PlayerState(own.PlayerId) { InitialDeckDefinitionIds=own.InitialDeckDefinitionIds };
        var firstSource=new CardInstance(Guid.NewGuid(),2,own.PlayerId) { CurrentZone=CardZone.Emblem };
        var laterSource=new CardInstance(Guid.NewGuid(),1,own.PlayerId) { CurrentZone=CardZone.Field };
        testOwner.Emblem.Add(firstSource);
        var game=new GameState("draw-reflection-plan",testOwner,enemy,own.PlayerId);
        const BindingFlags staticHidden=BindingFlags.Static|BindingFlags.NonPublic;
        var deck=typeof(AiStrategyPlanner).GetMethod("ReadDeck",staticHidden)!.Invoke(null,[game,own.PlayerId]);
        var deckKey=(string)typeof(AiStrategyPlanner).GetMethod("DeckKey",staticHidden)!.Invoke(null,[deck])!;
        var first=new CardInstanceDto{CardId=1,InstanceId=Guid.NewGuid(),OwnerPlayerId=own.PlayerId,Zone=CardZone.Hand,CurrentCost=1};
        var second=first with {CardId=2,InstanceId=Guid.NewGuid()};
        CardInstanceDto Visible(CardInstance card)=>new(){CardId=card.CurrentDefinitionId,InstanceId=card.InstanceId,
            OwnerPlayerId=card.OwnerPlayerId,Zone=card.CurrentZone};
        Task<PlayabilityResultDto> Legal(Guid uid,CancellationToken _)=>Task.FromResult(new PlayabilityResultDto
        {
            IsPlayable=uid==first.InstanceId || uid==second.InstanceId,SelectedPlayMode=PlayMode.Base,
            PlayModes=[new(){Mode=PlayMode.Base,IsAvailable=true,EffectiveCost=1}]
        });
        var reflectionPath=Path.Combine(planMemory,deckKey+".reflection.json");
        foreach (var (name,status,sourceStatus,chosenCard) in new[]
        {
            ("no-reflection", (string?)null, (string?)null, 1),
            ("same-short-deck", "requires-empty-hand", "present", 2),
            ("larger-deck", "different", "present", 1),
            ("source-gone", "different", "missing", 1),
            ("source-silenced", "different", "silenced", 1),
            ("source-unconfirmed", "unknown", "unknown", 1),
            ("sep-locked", "different", "present", 1),
            ("rules-changed", "different", "present", 1)
        })
        {
            if (status!=null)
            {
                var reflected=name=="rules-changed" ? grounded with
                {
                    Evidence=grounded.Evidence.Select(lesson=>lesson with { DefinitionRulesChanged=true }).ToArray()
                } : grounded;
                File.WriteAllText(reflectionPath,JsonSerializer.Serialize(reflected,json));
            }
            testOwner.Field.Clear();
            if (name is not ("source-gone" or "source-unconfirmed")) testOwner.Field.Add(laterSource);
            if (name=="source-silenced") laterSource.ApplySilence();
            else laterSource.RemoveSilence();
            var position=new GameStateDto
            {
                GameId=game.GameId,CurrentPlayerId=own.PlayerId,
                Players=[new(){PlayerId=own.PlayerId,TurnNumber=own.TurnNumber,CurrentPP=3,Hand=[first,second],
                    DeckCount=name=="larger-deck"?7:2,IsSuperEvolutionUnlocked=name!="sep-locked",
                    Emblem=[Visible(firstSource)],Field=name=="source-gone"?[]:[Visible(laterSource)]},
                    new(){PlayerId=enemy.PlayerId}]
            };
            using var planning=new AiStrategyPlanner(provider,planConfig,new TestHost(),NullLogger<AiStrategyPlanner>.Instance);
            provider.UseVerifiedDrawReflection=true;
            try
            {
                planning.WarmDeckAsync(game,own.PlayerId,"Havencraft")!.GetAwaiter().GetResult();
                var plan=planning.PlanAsync(game,position,own.PlayerId,default,Legal).GetAwaiter().GetResult();
                Check(plan?.Play is {Mode:"Base",Cost:1} play && play.CardId==chosenCard
                    && typeof(AiStrategyPlanner.TurnPlan).GetProperty("PlayBinding",hidden)!.GetValue(plan)!=null,
                    $"draw reflection changed the wrong bound plan in {name}");
                using var prompt=JsonDocument.Parse(provider.Bodies.Last());
                var experience=prompt.RootElement.GetProperty("experience");
                var matches=experience.GetProperty("drawReflectionMatches");
                if (status==null)
                {
                    Check(matches.ValueKind==JsonValueKind.Null,"missing reflection produced a position match");
                    continue;
                }
                var match=matches.EnumerateArray().Single();
                Check(match.GetProperty("status").GetString()==status
                    && match.GetProperty("insightIndex").GetInt32()==0
                    && match.GetProperty("gameIndex").GetInt32()==0 && match.GetProperty("actionIndex").GetInt32()==4
                    && match.GetProperty("currentHandCount").GetInt32()==2 && match.GetProperty("recordedHandCount").GetInt32()==0
                    && match.GetProperty("currentDeckCount").GetInt32()==position.Players[0].DeckCount
                    && match.GetProperty("sources")[1].GetProperty("status").GetString()==sourceStatus,
                    $"incorrect current comparison in {name}");
            }
            finally { provider.UseVerifiedDrawReflection=false; }
        }
    }

    private sealed class FakeProvider : HttpMessageHandler, IHttpClientFactory
    {
        public List<string> Bodies { get; } = new();
        public bool InvalidReflection;
        public string? ReflectionAnswer;
        public bool UseVerifiedDrawReflection;
        public bool InvalidGuide;
        public Action? BeforeOptionResponse;
        public TaskCompletionSource<bool>? GuideResponseGate;
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
            if (GuideResponseGate != null && data.RootElement.TryGetProperty("deck", out _)
                && !data.RootElement.TryGetProperty("own", out _) && !data.RootElement.TryGetProperty("options", out _))
                await GuideResponseGate.Task.WaitAsync(token);
            if (data.RootElement.TryGetProperty("options", out var candidates)
                && candidates[0].GetProperty("name").GetString() == "Timeout")
                throw new OperationCanceledException();
            var answer = data.RootElement.TryGetProperty("games", out _)
                ? (ReflectionAnswer ?? (InvalidReflection ? """[{"hypothesis":"unsupported","sources":[99]}]""" : """[{"hypothesis":"test resource conservation #2","sources":[0,1]}]"""))
                : data.RootElement.TryGetProperty("deck", out _) && !data.RootElement.TryGetProperty("own", out _) && !data.RootElement.TryGetProperty("options", out _)
                ? (InvalidGuide ? """{"gamePlan":"incomplete","cards":[]}"""
                    : """{"gamePlan":"plan","cards":[{"id":1,"role":"tempo"},{"id":2,"role":"payoff"}]}""")
                : data.RootElement.TryGetProperty("options", out var options)
                    ? (options[0].GetProperty("name").GetString() == "Invalid" ? """{"indexes":[9]}""" : """{"indexes":[0]}""")
                    : """{"focus":"pressure","plan":"play #2","prioritize":[2,1,999],"hold":[2,999],"ability":{"cardId":2,"kind":"act","cost":1,"name":"Draw","intent":"use"}}""";
            if (UseVerifiedDrawReflection && data.RootElement.TryGetProperty("legalPlays",out var legal))
            {
                var hypotheses=data.RootElement.GetProperty("experience").GetProperty("hypotheses");
                var matches=data.RootElement.GetProperty("experience").GetProperty("drawReflectionMatches");
                var loss=matches.ValueKind==JsonValueKind.Array && matches.EnumerateArray().Any(match=>
                    match.GetProperty("status").GetString() is ("similar" or "requires-empty-hand")
                    && hypotheses.GetProperty("insights")[match.GetProperty("insightIndex").GetInt32()]
                        .GetProperty("drawObservations").EnumerateArray().Any(observed=>
                        observed.GetProperty("gameIndex").GetInt32()==match.GetProperty("gameIndex").GetInt32()
                        && observed.GetProperty("actionIndex").GetInt32()==match.GetProperty("actionIndex").GetInt32()
                        && observed.GetProperty("deckExhausted").GetBoolean() && observed.GetProperty("ownDeckBefore").GetInt32()==2
                        && observed.GetProperty("exhaustedBatchIndex").GetInt32()==1
                        && observed.GetProperty("batches")[1].GetProperty("resolved").GetInt32()==0));
                var candidate=legal.EnumerateArray().Single(play=>play.GetProperty("cardId").GetInt32()==(loss?2:1));
                answer=JsonSerializer.Serialize(new {focus="setup",plan="offline draw reflection",prioritize=Array.Empty<int>(),hold=Array.Empty<int>(),
                    play=new {handIndex=candidate.GetProperty("handIndex").GetInt32(),cardId=candidate.GetProperty("cardId").GetInt32(),
                        mode="Base",cost=candidate.GetProperty("cost").GetInt32(),intent="use"}});
            }
            if (data.RootElement.TryGetProperty("options", out _)) BeforeOptionResponse?.Invoke();
            var envelope = JsonSerializer.Serialize(new { usage = new { prompt_tokens = 1000, prompt_cache_hit_tokens = 600, completion_tokens = 50 }, choices = new[] { new { message = new { content = answer } } } });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(envelope) };
        }
    }
}
