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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiLeaderRecoveryTests
{
    private const int Witch = 1474175, Melody = 1401301, Leader = 2000001535, Attacker = Leader + 1, Bait = Leader + 2, DeckCard = Leader + 3;
    private const int Alice = 1473469, StargazerEmblem = 1473946, Judge = 1474179, Reaction = Leader + 4;
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static void Run(string database, string output, string weights)
    {
        var original = GameDataProvider.Instance;
        var accessor = GameServiceProvider.ContextAccessor;
        using var catalog = new ServiceCollection().AddLogging().AddDbContext<ApplicationDbContext>(options => options.UseSqlite(
            new SqliteConnectionStringBuilder { DataSource = database, Mode = SqliteOpenMode.ReadOnly }.ToString()))
            .AddScoped<ICardRepository, EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
        try
        {
            var native = new GameDataProvider(catalog.GetRequiredService<IServiceScopeFactory>(), NullLogger<GameDataProvider>.Instance);
            native.InitializeAsync().GetAwaiter().GetResult();
            var cases = new[] { Scene(native, weights, Witch, false), Scene(native, weights, Witch, true),
                Scene(native, weights, Melody, false), Scene(native, weights, Melody, true),
                Scene(native, weights, Witch, true, locked: true), Scene(native, weights, Witch, true, threat: 7),
                Scene(native, weights, Melody, true, mixedReactions: true, scoped: false),
                Scene(native, weights, Melody, true, mixedReactions: true),
                Scene(native, weights, Melody, true, mixedReactions: true, blocker: EventType.Heal),
                Scene(native, weights, Melody, true, mixedReactions: true, blocker: EventType.PlayCard) };
            File.WriteAllText(output, JsonSerializer.Serialize(new { providerRequests = 0, cases }, new JsonSerializerOptions(Json) { WriteIndented = true }));
        }
        finally { GameDataProvider.SetInstanceForTesting(original); GameServiceProvider.ContextAccessor = accessor; }
    }

    private static object Scene(IGameDataProvider native, string weights, int recoveryCard, bool enabled, bool locked = false, int threat = 4,
        bool mixedReactions = false, bool scoped = true, EventType? blocker = null)
    {
        var definitions = native.GetAllCardDefinitions().ToDictionary(pair => pair.Key, pair => pair.Value);
        definitions[Leader] = new() { Id = Leader, Name = "fixture leader", Type = CardType.Leader, Health = 20 };
        definitions[Attacker] = new() { Id = Attacker, Name = "fixture attacker", Type = CardType.Follower, Attack = threat - (mixedReactions ? 1 : 0), Health = 12 };
        definitions[Bait] = new() { Id = Bait, Name = "fixture alternative", Type = CardType.Spell, Cost = 3 };
        definitions[DeckCard] = new() { Id = DeckCard, Name = "fixture draw", Type = CardType.Spell, Cost = 20 };
        definitions[Reaction] = new() { Id = Reaction, Name = "fixture reaction", Type = CardType.Crest };
        var reactions = blocker is EventType eventType ? new List<AbilityConfig> {
            new() { CardId = Reaction, Name = "prevent recovery", Type = AbilityType.Triggered,
                Trigger = new() { EventTypes = [eventType], Timing = TriggerTiming.Before, ValidZones = [CardZone.Emblem] },
                Conditions = eventType == EventType.PlayCard
                    ? [ConditionConfig.FromExpression("(context.triggeringEvent.PlayedCard.currentDefinition.id) == (1401301)")] : [],
                Effects = [new() { Type = EffectType.PreventEvent }]
            }
        } : [];
        var data = Proxy<IGameDataProvider>.Make((method, args) => method.Name switch {
            "GetCardDefinition" => definitions.GetValueOrDefault((int)args![0]!), "GetAllCardDefinitions" => definitions,
            "GetAbilityConfigs" when (int)args![0]! == Reaction => reactions,
            "GetAbilityConfigs" => (int)args![0]! >= Leader ? new List<AbilityConfig>() : native.GetAbilityConfigs((int)args[0]!),
            _ => method.Invoke(native, args)
        });
        GameDataProvider.SetInstanceForTesting(data);
        var accessor = new GameContextAccessor();
        GameServiceProvider.ContextAccessor = accessor;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AiStrategy:Enabled"] = "true", ["AiStrategy:Endpoint"] = "https://example.invalid/chat/completions",
            ["AiStrategy:Model"] = "offline-recovery-fixture", ["AiStrategy:ProjectLeaderRecovery"] = enabled.ToString(),
            ["AiStrategy:ScopeRecoveryReactions"] = scoped.ToString(),
            ["AiStrategy:GuideCacheEnabled"] = "false", ["AiModel:Path"] = Path.GetFullPath(weights),
            ["AiStrategy:MemoryPath"] = Path.Combine(Path.GetTempPath(), "ai-leader-recovery-" + Guid.NewGuid().ToString("N"))
        }).Build();
        var host = new Host();
        using var provider = new Provider();
        var model = new AiLearningModel(host, config, NullLogger<AiLearningModel>.Instance);
        Check(model.Current != null, "missing frozen model");
        GameState game = null!;
        GameService preflight = null!;
        ServiceProvider services = null!;
        var actions = new List<string>();
        var health = new List<int>();

        async Task<PlayabilityResultDto> CheckCard(Guid uid)
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;
            var card = game.FindCardInstance(uid)!;
            using var context = new GameContext(sp, sp.GetRequiredService<IMapper>(), accessor, game, game.Player1.PlayerId);
            return await (Task<PlayabilityResultDto>)typeof(GameService).GetMethod("CheckPlayabilityCoreLogicAsync", Hidden)!
                .Invoke(preflight, [context with { SourceCard = card }, game.Player1, card, definitions[card.CurrentDefinitionId], PlayMode.Base])!;
        }

        async Task Resolve(IGameEvent ev, Guid actor)
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;
            using var context = new GameContext(sp, sp.GetRequiredService<IMapper>(), accessor, game, actor);
            var result = await sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context, [ev]);
            Check(result.IsSuccess && !result.IsCancelled, result.ErrorMessage ?? "native event failed");
            services.GetRequiredService<AiStrategyPlanner>().ObserveCommittedEvents(game, result.ProcessedEvents);
        }

        async Task<bool> Commit(PlayerActionWrapper action)
        {
            IGameEvent ev = action.ActionData switch {
                PlayCardActionDto play => new PlayCardRequestedEvent(action.PlayerId, play),
                EvolveActionDto evolve => new EvolveRequestedEvent(action.PlayerId, evolve),
                AttackActionDto attack => new AttackRequestedEvent(action.PlayerId, attack),
                _ when action.ActionType == "EndTurn" => new EndTurnRequestedEvent(action.PlayerId),
                _ => throw new InvalidOperationException(action.ActionType)
            };
            var suffix = action.ActionData is PlayCardActionDto playCard ? ":" + game.FindCardInstance(playCard.CardUid)!.BaseDefinitionId : "";
            actions.Add(action.ActionType + suffix);
            await Resolve(ev, action.PlayerId);
            health.Add(game.Player1.Leader!.CurrentHealth);
            action.MarkCompleted();
            return true;
        }

        var games = Proxy<IGameService>.Make((method, args) => method.Name switch {
            "GetGameState" => game,
            "GetCardPlayabilityAsync" or "GetCardPlayabilityForModeAsync" => CheckCard((Guid)args![2]!),
            "EnqueuePlayerActionAsync" => Commit((PlayerActionWrapper)args![1]!),
            _ => null
        });
        var pending = Proxy<IPendingActionService>.Make((method, _) => method.ReturnType == typeof(bool) ? false : null);
        using (services = AiEndTurnEngineTests.Services(data, accessor, pending, games, registration => {
            registration.AddSingleton<IConfiguration>(config).AddSingleton<IHostEnvironment>(host)
                .AddSingleton<IHttpClientFactory>(provider).AddSingleton(model).AddSingleton<AiStrategyPlanner>();
        }))
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;
            preflight = new GameService(Proxy<Microsoft.AspNetCore.SignalR.IHubContext<ShadowverseBeyond.Server.Hubs.GameHub>>.Make((_, _) => null),
                NullLogger<GameService>.Instance, sp.GetRequiredService<IMapper>(), Proxy<IClientMethodMapper>.Make((_, _) => null),
                data, Proxy<IMatchService>.Make((_, _) => null), Proxy<IFirstPlayerSelector>.Make((_, _) => null),
                services.GetRequiredService<IServiceScopeFactory>(), pending, Proxy<IGameStatsRuntimeTracker>.Make((_, _) => null));
            var own = new PlayerState(Guid.NewGuid()) { IsAI = true, TurnNumber = 5, CurrentPlayPoints = 3, MaxPlayPoints = 5,
                IsEvolutionUnlocked = !locked, CurrentEvolutionPoints = 1, MaxEvolutionPoints = 1, InitialDeckDefinitionIds = [recoveryCard, Bait, DeckCard] };
            var enemy = new PlayerState(Guid.NewGuid()) { TurnNumber = 5 };
            game = new GameState("leader-recovery-fixture", own, enemy, own.PlayerId) { IsLocalAiTraining = true,
                Phase = GamePhase.Main, Status = GameStatus.InProgress, AbilityManager = sp.GetRequiredService<IAbilityManager>(),
                BuffManager = sp.GetRequiredService<IBuffManager>() };
            typeof(GameState).GetProperty("StrategyBenchmarkPlayerId", Hidden)!.SetValue(game, own.PlayerId);
            var evaluator = typeof(AiTurnExecutor).Assembly.GetType("ShadowverseBeyond.Server.Services.AiLeaderRecoveryEvaluation")!;
            evaluator.GetMethod("Bind", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [game, own.PlayerId]);
            var factory = sp.GetRequiredService<ICardFactory>();
            CardInstance Add(PlayerState player, int id, CardZone zone) => factory.CreateAndRegisterNewCard(game, id, player.PlayerId, zone);
            own.Leader = Add(own, Leader, CardZone.Field);
            own.Leader.Damage = 17;
            enemy.Leader = Add(enemy, Leader, CardZone.Field);
            Add(own, recoveryCard, CardZone.Hand);
            Add(own, Bait, CardZone.Hand);
            var attacker = Add(enemy, Attacker, CardZone.Field);
            attacker.ZoneStayTurn = 1;
            CardInstance? alice = null;
            if (mixedReactions)
            {
                alice = Add(enemy, Alice, CardZone.Field);
                alice.ZoneStayTurn = 1;
                Add(own, StargazerEmblem, CardZone.Emblem);
                Add(own, Judge, CardZone.Hand);
                Add(own, 1471482, CardZone.Field);
            }
            if (blocker.HasValue) Add(enemy, Reaction, CardZone.Emblem);
            for (var i = 0; i < 6; i++) { Add(own, DeckCard, CardZone.Deck); Add(enemy, DeckCard, CardZone.Deck); }
            var ai = new AiService(NullLogger<AiService>.Instance, services.GetRequiredService<IServiceScopeFactory>(), model, config);
            var executor = new AiTurnExecutor(games, ai, NullLogger<AiTurnExecutor>.Instance, sp.GetRequiredService<IMapper>(), services, model, config);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            executor.ExecuteTurnAsync(game.GameId, own.PlayerId, timeout.Token).GetAwaiter().GetResult();
            var canVerify = !locked && threat == 4 && blocker == null && (!mixedReactions || scoped);
            var shouldRecover = enabled && canVerify;
            Check(actions.First() == "PlayCard:" + (shouldRecover ? recoveryCard : Bait),
                 $"{recoveryCard} enabled={enabled} locked={locked} threat={threat} proposedRecovery={provider.Recovery}: " + string.Join(", ", actions));
            Check(provider.Plans == 1 && provider.Recovery == (shouldRecover ? 3 : 0), "planning input lost native recovery bounds");
            if (shouldRecover && recoveryCard == Witch) Check(actions[1] == "PerformEvolve" && health[1] == 6 && own.CurrentEvolutionPoints == 0,
                "witch recovery was not followed by normal evolution and a native 3-point heal: "
                + string.Join(", ", actions) + " health=" + string.Join(",", health) + " ep=" + own.CurrentEvolutionPoints);
            if (shouldRecover && recoveryCard == Melody) Check(health[0] == 6 && own.CurrentEvolutionPoints == 1,
                "melody did not settle a 3-point heal without evolution points");
            Resolve(new AttackRequestedEvent(enemy.PlayerId, new AttackActionDto { AttackerUid = attacker.InstanceId, TargetUid = own.Leader.InstanceId }),
                enemy.PlayerId).GetAwaiter().GetResult();
            if (alice != null && !game.IsGameOver)
                Resolve(new AttackRequestedEvent(enemy.PlayerId, new AttackActionDto { AttackerUid = alice.InstanceId, TargetUid = own.Leader.InstanceId }),
                    enemy.PlayerId).GetAwaiter().GetResult();
            Check(shouldRecover ? own.Leader.CurrentHealth == 2 && !game.IsGameOver : game.WinnerId == enemy.PlayerId,
                "same visible attack did not reproduce expected survival/defeat");
            var instance = evaluator.GetMethod("For", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [game]);
            var observation = (AiLeaderRecoveryMatchFacts)evaluator.GetMethod("Snapshot", Hidden)!.Invoke(instance, [])!;
            var appliedSteps = shouldRecover ? (recoveryCard == Witch ? 2 : 1) : 0;
            var observedRoutes = canVerify ? Math.Max(1, appliedSteps) : 0;
            Check(observation.Checks > 0 && observation.VerifiedRoutes == observedRoutes
                && observation.CompletedSteps == appliedSteps
                && observation.HpGained == (shouldRecover ? 3 : 0), "recovery evaluation did not match completed native actions");
            if (blocker.HasValue)
                Check(observation.BlockingExamples.Any(example => example.BlockingCardId == Reaction
                    && example.Events.Contains(blocker.Value.ToString())), "relevant reaction was incorrectly ignored");
            return new { recoveryCard, enabled, locked, threat, mixedReactions, scoped, blocker = blocker?.ToString(), actions, health, observation, plannerRecovery = provider.Recovery,
                healthAfterNativeAttack = own.Leader.CurrentHealth, survives = !game.IsGameOver, passed = true };
        }
    }

    private static void Check(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }

    private sealed class Provider : HttpMessageHandler, IHttpClientFactory
    {
        internal int Plans, Recovery;
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            using var envelope = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var input = new Dictionary<string, JsonElement>();
            foreach (var message in envelope.RootElement.GetProperty("messages").EnumerateArray().Where(item => item.GetProperty("role").GetString() == "user"))
            {
                using var fields = JsonDocument.Parse(message.GetProperty("content").GetString()!);
                foreach (var field in fields.RootElement.EnumerateObject()) input[field.Name] = field.Value.Clone();
            }
            object answer;
            if (input.TryGetValue("legalPlays", out var plays))
            {
                Plans++;
                Recovery = input["localTactics"].GetProperty("leaderRecovery").GetInt32();
                var bait = plays.EnumerateArray().Single(play => play.GetProperty("cardId").GetInt32() == Bait);
                answer = new AiStrategyPlanner.TurnPlan("stabilize", "模拟接口建议先花费费用过渡。", [], [],
                    Play: new(bait.GetProperty("handIndex").GetInt32(), Bait, "Base", 3, "use"));
            }
            else answer = new AiStrategyPlanner.DeckGuide("模拟攻略", input["deck"].EnumerateArray()
                .Select(card => new AiStrategyPlanner.CardRole(card.GetProperty("id").GetInt32(), "按规则核对")).ToArray());
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(
                new { choices = new[] { new { message = new { content = JsonSerializer.Serialize(answer, Json) } } } })) };
        }
    }

    private sealed class Host : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Offline recovery test";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
