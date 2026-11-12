using System.Net;
using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiPlannedModeTests
{
    private const int Source = 2000001501, Token = 2000001502, Drawn = 2000001503;
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Type Modes = typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiPlannedModes")!;
    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
    private static object Fixture(string method, params object[] arguments) =>
        typeof(AiDecisionFlowTests).GetMethod(method, Hidden)!.Invoke(null, arguments)!;
    private static void Planner(AiStrategyPlanner planner, string method, params object?[] arguments) =>
        typeof(AiStrategyPlanner).GetMethod(method, Hidden)!.Invoke(planner, arguments);
    private static IReadOnlyDictionary<Guid, AbilityConfig> Options(PlayerChoiceRequest request) =>
        (IReadOnlyDictionary<Guid, AbilityConfig>)typeof(PlayerChoiceRequest).GetProperty("DecisionOptions", Hidden)!.GetValue(request)!;

    internal static void Run(string output)
    {
        PrivacyBoundary();
        var first = new AiStrategyPlanner.PlayAdvice(0, Source, "Base", 1, "use", Choices: [new(Source, 1, 0, 0)]);
        var copy = JsonSerializer.Deserialize<AiStrategyPlanner.PlayAdvice>(JsonSerializer.Serialize(first, Json), Json)!;
        Check((bool)Modes.GetMethod("Same", Hidden)!.Invoke(null, [first, copy])!, "equal deserialized branch arrays failed plan equality");
        Check(!(bool)Modes.GetMethod("Same", Hidden)!.Invoke(null,
            [first, copy with { Choices = [new(Source, 1, 0, 1)] }])!, "different choices were treated as the same play");
        var cases = new List<object>();
        foreach (var kind in new[] { "legacy", "planned", "reversed", "completed-root", "changed-parent", "changed-branch", "dynamic-repeat", "invalid-hint", "partial-hint", "changed-scene", "changed-catalog", "catalog-during-root" })
            cases.Add(Execute(kind));
        File.WriteAllText(output, JsonSerializer.Serialize(new { realProviderRequests = 0, cases,
            scope = "Scripted planning branches execute real native source and repeated choices. This proves branch handoff and fallback, not real LLM strategic strength." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS planned mode choices: 12 native transactions, distinct repeated branches, reordered indices, completed root, changed scene/rules, dynamic stages, invalid/partial hints and provenance; no real provider requests");
    }

    private static void PrivacyBoundary()
    {
        using var fixture = new AiActionOutcomeTests.Fixture();
        var manager = fixture.Game.AbilityManager;
        var reads = 0;
        fixture.Game.AbilityManager = Proxy<IAbilityManager>.Make((method, _) =>
        {
            if (method.Name == "GetEffectiveAbilitiesForCard") reads++;
            return new List<IAbility>();
        });
        try
        {
            var hidden = new CardInstanceDto { InstanceId = fixture.Hidden.InstanceId, CardId = 2000000999,
                OwnerPlayerId = fixture.Enemy.PlayerId, Zone = CardZone.Hand };
            var candidateType = typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiPlayCandidates+Candidate")!;
            var candidate = Activator.CreateInstance(candidateType, hidden, hidden, 0, PlayMode.Base, 0, false, null, hidden.CardId);
            var stages = (Array)Modes.GetMethod("Read", Hidden)!.Invoke(null, [fixture.Game, candidate])!;
            Check(stages.Length == 0 && reads == 0, "foreign hidden-hand candidate read abilities before ownership validation");
        }
        finally { fixture.Game.AbilityManager = manager; }
    }

    internal static void RunReal(string database, string output)
    {
        var previous = GameDataProvider.Instance;
        using var catalog = new ServiceCollection().AddLogging()
            .AddDbContext<ApplicationDbContext>(options => options.UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = database, Mode = SqliteOpenMode.ReadOnly }.ToString()))
            .AddScoped<ICardRepository, EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
        try
        {
            var native = new GameDataProvider(catalog.GetRequiredService<IServiceScopeFactory>(), NullLogger<GameDataProvider>.Instance);
            native.InitializeAsync().GetAwaiter().GetResult();
            var definitions = native.GetAllCardDefinitions().ToDictionary(pair => pair.Key, pair => pair.Value);
            definitions[2000001500] = new() { Id = 2000001500, Type = CardType.Leader, Health = 20 };
            definitions[Drawn] = new() { Id = Drawn, Type = CardType.Spell, Cost = 9, Name = "material" };
            definitions[2000001504] = new() { Id = 2000001504, Type = CardType.Spell, Cost = 7, Name = "PRIVATE_ENEMY_HAND" };
            var data = Proxy<IGameDataProvider>.Make((method, args) => method.Name switch
            {
                "GetCardDefinition" => definitions.GetValueOrDefault((int)args![0]!),
                "GetAllCardDefinitions" => definitions,
                "GetAbilityConfigs" when (int)args![0]! >= 2000001500 => new List<AbilityConfig>(),
                _ => method.Invoke(native, args)
            });
            var rows = new List<object>();
            foreach (var kind in new[] { "native-stars-legacy", "native-stars", "native-tutor-legacy", "native-tutor" })
                rows.Add(Execute(kind, data, kind.Contains("stars") ? 1472978 : 1474113));
            File.WriteAllText(output, JsonSerializer.Serialize(new { realProviderRequests = 0, cases = rows,
                scope = "Loaded, unchanged real Star Call and Tutor configurations with scripted plans and exact native effects. Validates root-bound mode intent and fewer requests, not real LLM win-rate strength." }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("PASS real-card planned modes: Star Call's three skeletons and Tutor's Reiss search, four old/new native transactions; no real provider requests");
        }
        finally { GameDataProvider.SetInstanceForTesting(previous); }
    }

    private static GameState RealGame(IServiceProvider services, int sourceId)
    {
        var own = new PlayerState(Guid.NewGuid()) { IsAI = true, TurnNumber = 6,
            CurrentPlayPoints = 4, MaxPlayPoints = 4, InitialDeckDefinitionIds = sourceId == 1474113 ? [sourceId, 1474120, Drawn] : [sourceId] };
        var enemy = new PlayerState(Guid.NewGuid()) { TurnNumber = 6 };
        var game = new GameState("real-planned-modes", own, enemy, own.PlayerId) { Phase = GamePhase.Main, Status = GameStatus.InProgress,
            AbilityManager = services.GetRequiredService<IAbilityManager>(),
            BuffManager = services.GetRequiredService<ShadowverseBeyond.Server.Domain.Buffs.IBuffManager>() };
        var factory = services.GetRequiredService<ICardFactory>();
        own.Leader = factory.CreateAndRegisterNewCard(game, 2000001500, own.PlayerId, CardZone.Field);
        enemy.Leader = factory.CreateAndRegisterNewCard(game, 2000001500, enemy.PlayerId, CardZone.Field);
        factory.CreateAndRegisterNewCard(game, sourceId, own.PlayerId, CardZone.Hand);
        factory.CreateAndRegisterNewCard(game, 2000001504, enemy.PlayerId, CardZone.Hand);
        if (sourceId == 1474113)
        {
            factory.CreateAndRegisterNewCard(game, Drawn, own.PlayerId, CardZone.Hand);
            factory.CreateAndRegisterNewCard(game, 1474120, own.PlayerId, CardZone.Deck);
            // Keep the unrelated automatic 'establish missing engine' override out of this branch handoff control.
            factory.CreateAndRegisterNewCard(game, 1474276, own.PlayerId, CardZone.Emblem);
        }
        return game;
    }

    private static object Execute(string kind, IGameDataProvider? supplied = null, int sourceId = Source)
    {
        var previous = GameDataProvider.Instance;
        var priorAccessor = GameServiceProvider.ContextAccessor;
        var parent = (AbilityConfig)Fixture("Parent");
        if (kind == "dynamic-repeat") parent.Effects[1].RepeatCountValue = new StaticValueConfig { Value = 1 };
        var originalData = supplied ?? (IGameDataProvider)Fixture("Data", parent);
        var revision = originalData.DataRevision;
        var data = kind.Contains("catalog") ? Proxy<IGameDataProvider>.Make((method, args) =>
            method.Name == "get_DataRevision" ? revision : method.Invoke(originalData, args)) : originalData;
        GameDataProvider.SetInstanceForTesting(data);
        var accessor = new GameContextAccessor();
        GameServiceProvider.ContextAccessor = accessor;
        var directory = Path.Combine(Path.GetTempPath(), "ai-planned-mode-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiStrategy:Enabled"] = "true", ["AiStrategy:Model"] = "scripted-mode",
                ["AiStrategy:GuideCacheEnabled"] = "false",
                ["AiStrategy:Endpoint"] = "https://example.invalid/chat/completions",
                ["AiStrategy:MemoryPath"] = directory, ["AiStrategy:PlannedModeChoices"] = (!kind.Contains("legacy")).ToString(),
                ["AiModel:Path"] = Path.Combine(directory, "missing-model.json")
            }).Build();
            using var http = new Provider(kind);
            var model = new AiLearningModel(new Host(), config, NullLogger<AiLearningModel>.Instance);
            using var planner = new AiStrategyPlanner(http, config, new Host(), NullLogger<AiStrategyPlanner>.Instance, model);
            GameState game = null!;
            ServiceProvider services = null!;
            AiService ai = null!;
            var selected = new List<string>();
            var games = Proxy<IGameService>.Make((method, _) => method.Name == "GetGameState" ? game : null);
            var pending = Proxy<IPendingActionService>.Make((method, args) =>
            {
                if (method.ReturnType == typeof(bool)) return false;
                if (method.Name != "WaitForPlayerChoiceAsync") return null;
                var request = (PlayerChoiceRequest)args![1]!;
                if (kind == "reversed") { request.SelectableTargetIds.Reverse(); request.SelectableCardInstances.Reverse(); }
                if (kind == "changed-branch")
                    foreach (var option in Options(request).Values) option.Description += "runtime changed";
                using var scope = services.CreateScope();
                using var context = new GameContext(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<IMapper>(), accessor, game, game.Player1.PlayerId);
                var snapshot = GameStateProjection.Create(game, context);
                if (request.TargetKey != "decision")
                    return Task.FromResult(ai.DecideTargetChoiceAsync(request, snapshot).GetAwaiter().GetResult());
                Check(game.Player1.Leader.CurrentHealth == (supplied == null ? kind == "changed-scene" ? 7 : 8 : 20) && game.Player1.CurrentPlayPoints == 4,
                    "preflight executed prefix damage/payment before decision");
                var choice = ai.DecideTargetChoiceAsync(request, snapshot).GetAwaiter().GetResult();
                var calls = http.Inputs.Count;
                var repeat = ai.DecideTargetChoiceAsync(request, snapshot).GetAwaiter().GetResult();
                var changed = http.Inputs.Count > calls ? http.Inputs[^1].EnumerateObject()
                    .Where(field => http.Inputs[calls - 1].TryGetProperty(field.Name, out var old) && old.GetRawText() != field.Value.GetRawText())
                    .Select(field => field.Name).ToArray() : [];
                Check(repeat.ChosenTargetIds.SequenceEqual(choice.ChosenTargetIds) && http.Inputs.Count == calls,
                    $"repeated request {kind} made another provider request: {calls}->{http.Inputs.Count}, changed={string.Join(',', changed)}, counts={request.MinSelectionCount}/{request.MaxSelectionCount}, metrics={JsonSerializer.Serialize(typeof(AiStrategyPlanner).GetMethod("GetDecisionMetrics", Hidden)!.Invoke(planner, [game]), new JsonSerializerOptions { IncludeFields = true })}");
                var branch = Options(request)[choice.ChosenTargetIds.Single()];
                var label = branch.Effects[0].Type.ToString();
                if (kind.Contains("stars")) label += ":" + typeof(AiService).Assembly
                    .GetType("ShadowverseBeyond.Server.Services.AiGeneratedTokenTactics")!.GetMethod("Token", Hidden)!.Invoke(null, [branch]);
                selected.Add(label);
                return Task.FromResult(choice);
            });
            using (services = AiEndTurnEngineTests.Services(data, accessor, pending, games, items => items.AddSingleton(planner)))
            {
                using var scope = services.CreateScope();
                var sp = scope.ServiceProvider;
                game = supplied == null ? (GameState)Fixture("Game", sp, accessor) : RealGame(sp, sourceId);
                var source = game.Player1.Hand.First();
                ai = new(NullLogger<AiService>.Instance, services.GetRequiredService<IServiceScopeFactory>(), model, config);
                planner.WarmDeckAsync(game, game.Player1.PlayerId, "Neutral")!.GetAwaiter().GetResult();
                using var context = new GameContext(sp, sp.GetRequiredService<IMapper>(), accessor, game, game.Player1.PlayerId);
                var before = GameStateProjection.Create(game, context);
                var plan = planner.PlanAsync(game, before, game.Player1.PlayerId, default, (uid, _) =>
                    Task.FromResult(new PlayabilityResultDto { IsPlayable = uid == source.InstanceId, SelectedPlayMode = PlayMode.Base,
                        PlayModes = [new() { Mode = PlayMode.Base, EffectiveCost = source.CurrentCost, IsAvailable = true }] })).GetAwaiter().GetResult();
                var repeats = kind.Contains("stars") ? 3 : kind.Contains("tutor") ? 1 : 2;
                Check(plan?.Play?.Choices?.Length == (kind == "partial-hint" ? 1 : repeats), "scripted explicit mode plan not accepted");
                if (kind == "changed-parent") parent.Effects[0].Parameters["DamageAmount"] = new StaticValueConfig { Value = 2 };
                if (kind == "changed-scene") game.Player1.Leader.Damage++;
                if (kind == "changed-catalog") revision = Guid.NewGuid();
                var cost = source.CurrentCost;
                var action = new PlayCardActionDto { CardUid = source.InstanceId, PlayMode = PlayMode.Base, PaidCost = cost };
                var root = new PlayerActionWrapper("PlayCard", game.Player1.PlayerId, action);
                Planner(planner, "BeginActionOutcome", game, root, before, null);
                if (kind == "completed-root") root.MarkCompleted();
                if (kind == "catalog-during-root") revision = Guid.NewGuid();
                var result = sp.GetRequiredService<IGameActionTransactionService>()
                    .ProcessTransactionAsync(context, [new PlayCardRequestedEvent(game.Player1.PlayerId, action)]).GetAwaiter().GetResult();
                Check(result.IsSuccess && !result.IsCancelled, result.ErrorMessage ?? "native mode transaction failed");
                planner.ObserveCommittedEvents(game, result.ProcessedEvents);
                root.MarkCompleted();
                Planner(planner, "EndActionOutcome", game, root);
                planner.ObserveCommittedEvents(game, [new GameEndEvent(game.Player1.PlayerId, game.Player2.PlayerId, "mode handoff fixture")]);
                var planned = kind is "planned" or "reversed" or "native-stars" or "native-tutor";
                var expected = kind.Contains("stars") ? Enumerable.Repeat(planned ? "GetToken:1250402" : "GetToken:1050403", 3).ToArray()
                    : kind.Contains("tutor") ? [planned ? "Draw" : "Summon"]
                    : planned ? ["GetToken", "Draw"] : new[] { "GetToken", "GetToken" };
                Check(selected.SequenceEqual(expected),
                    "native mode handoff/fallback mismatch: " + kind + " -> " + string.Join(',', selected));
                var modeCalls = http.Inputs.Count(input => input.TryGetProperty("options", out _));
                Check(modeCalls == (planned ? 0 : kind == "partial-hint" ? 1 : repeats), "mode requests were not avoided/fallback restored: " + kind);
                if (kind.Contains("stars")) Check(game.Player1.Hand.Count == 3
                    && game.Player1.Hand.All(card => card.CurrentDefinitionId == (planned ? 1250402 : 1050403))
                    && game.Player1.CurrentPlayPoints == 2 && game.Player1.Leader.CurrentHealth == 17, "actual Star Call result wrong");
                else if (kind.Contains("tutor")) Check(game.Player1.Hand.Any(card => card.CurrentDefinitionId == 1474120) == planned
                    && game.Player1.Field.Count == (planned ? 0 : 2) && game.Player1.CurrentPlayPoints == 3, "actual Tutor search/summon result wrong");
                else Check(game.Player1.Hand.Count == 2 && game.Player1.Hand.Count(card => card.CurrentDefinitionId == Token) == (planned ? 1 : 2)
                    && game.Player1.CurrentPlayPoints == 3 && game.Player1.Leader.CurrentHealth == (kind == "changed-parent" ? 6 : kind == "changed-scene" ? 4 : 5),
                    "native resulting hand/resources contradicted selected branches");
                var history = Directory.GetFiles(directory, "*.json").Where(path => Path.GetFileNameWithoutExtension(path).Length == 64)
                    .SelectMany(path => JsonSerializer.Deserialize<AiStrategyPlanner.Lesson[]>(File.ReadAllText(path), Json)!).Single();
                Check(history.Choices?.Length == repeats && history.Choices.All(choice => choice.ModelSuggested == true),
                    "committed choices lost model provenance");
                Check(history.Choices!.All(choice => choice.Comparison is { LocalIndexes.Length: 1 } comparison
                    && comparison.Different == !choice.Selected.SequenceEqual(comparison.LocalIndexes)),
                    "committed modes did not join their local candidates by actual option identity: " + kind);
                Check(history.Recording!.EventTotals.GetValueOrDefault("differentCommittedOptions")
                    == history.Choices!.Count(choice => choice.Comparison?.Different == true),
                    "repeated requests duplicated committed choice impact");
                var captured = JsonSerializer.Serialize(http.Inputs, Json);
                Check(!captured.Contains("PRIVATE_ENEMY_HAND") && !captured.Contains(game.Player2.PlayerId.ToString())
                    && !captured.Contains(source.InstanceId.ToString()), "mode context exposed hidden/private instance data");
                return new { kind, selected, modeCalls, totalScriptedRequests = http.Inputs.Count, nativeSuccess = result.IsSuccess,
                    hand = game.Player1.Hand.Select(card => card.CurrentDefinitionId).ToArray(), passed = true };
            }
        }
        finally
        {
            GameDataProvider.SetInstanceForTesting(previous);
            GameServiceProvider.ContextAccessor = priorAccessor;
            Check(Path.GetFullPath(directory).StartsWith(Path.Combine(Path.GetTempPath(), "ai-planned-mode-"), StringComparison.OrdinalIgnoreCase), "unexpected cleanup path");
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private sealed class Provider(string kind) : HttpMessageHandler, IHttpClientFactory
    {
        internal readonly List<JsonElement> Inputs = [];
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            using var envelope = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var fields = new Dictionary<string, JsonElement>();
            foreach (var message in envelope.RootElement.GetProperty("messages").EnumerateArray().Where(m => m.GetProperty("role").GetString() == "user"))
            {
                using var part = JsonDocument.Parse(message.GetProperty("content").GetString()!);
                foreach (var field in part.RootElement.EnumerateObject()) fields[field.Name] = field.Value.Clone();
            }
            var input = JsonSerializer.SerializeToElement(fields, Json);
            Inputs.Add(input);
            object answer;
            if (input.TryGetProperty("legalPlays", out var plays))
            {
                var candidate = plays[0];
                var stages = candidate.GetProperty("modeStages");
                var repeats = kind.Contains("stars") ? 3 : kind.Contains("tutor") ? 1 : 2;
                if (kind != "dynamic-repeat") Check(stages.GetArrayLength() == 1 && stages[0].GetProperty("executions").GetInt32() == repeats,
                    "native repeated stages missing from legal candidate");
                var ability = stages.ValueKind == JsonValueKind.Array ? stages[0].GetProperty("abilityDefinitionId").GetInt64() : Source;
                var hints = Enumerable.Range(0, repeats).Select(index => new AiStrategyPlanner.ModeAdvice(ability, 1, index,
                    kind.Contains("native-") ? 1 : kind == "invalid-hint" && index == 1 ? 99 : index)).ToArray();
                if (kind == "partial-hint") hints = hints[..1];
                var advice = new AiStrategyPlanner.PlayAdvice(0, candidate.GetProperty("cardId").GetInt32(), "Base",
                    candidate.GetProperty("cost").GetInt32(), "use", Choices: hints);
                answer = new AiStrategyPlanner.TurnPlan("setup", "explicit offline branches", [], [], Play: advice, Plays: [advice]);
            }
            else if (input.TryGetProperty("options", out var options))
            {
                var local = input.GetProperty("localCandidateIndexes");
                Check(local.ValueKind == JsonValueKind.Array && local.GetArrayLength() == 1
                    && local[0].GetInt32() >= 0 && local[0].GetInt32() < options.GetArrayLength(),
                    "production option path omitted its legal local candidate");
                var rules = input.GetProperty("decisionRules").GetProperty("rules");
                var option = options.EnumerateArray().First(option => rules[option.GetProperty("rule").GetInt32()]
                    .GetProperty("effects")[0].GetProperty("type").GetString() == (kind.Contains("tutor") ? "Summon" : "GetToken"));
                answer = new { indexes = new[] { option.GetProperty("index").GetInt32() } };
            }
            else answer = new AiStrategyPlanner.DeckGuide("offline guide", input.GetProperty("deck").EnumerateArray()
                .Select(card => new AiStrategyPlanner.CardRole(card.GetProperty("id").GetInt32(), "fixture")).ToArray());
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
            { choices = new[] { new { message = new { content = JsonSerializer.Serialize(answer, Json) } } } })) };
        }
    }
    private sealed class Host : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "offline";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = ".";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
