using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Services;

internal static class AiSearchValueTests
{
    private static readonly Type Search = typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTacticalSearch")!;
    private static readonly MethodInfo Find = Search.GetMethod("FindWithValue", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static object? Plan(GameStateDto state, Guid actor, Func<GameStateDto, double>? value)
        => Find.Invoke(null, [state, actor, CancellationToken.None, null, null, null, null, value]);
    private static T Field<T>(object value, string name) => (T)value.GetType().GetProperty(name)!.GetValue(value)!;
    private static AttackActionDto Attack(object plan) => Field<AttackActionDto>(Field<object>(plan, "First"), "Data");

    private static void CleanupFixture(string path, string prefix)
    {
        var resolved = Path.GetFullPath(path);
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(resolved).StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected offline fixture directory");
        if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
    }

    internal static void BoundedDecisions()
    {
        var own = Guid.NewGuid(); var enemy = Guid.NewGuid();
        CardInstanceDto Leader(Guid actor, int hp) => new() { InstanceId = Guid.NewGuid(), OwnerPlayerId = actor,
            Type = CardType.Leader, CurrentHealth = hp, MaxHealth = 20 };
        var unit = new CardInstanceDto { InstanceId = Guid.NewGuid(), OwnerPlayerId = own, Type = CardType.Follower,
            Zone = CardZone.Field, CurrentAttack = 3, CurrentHealth = 3, MaxHealth = 3, CanAttack = true,
            MaxAttacksPerTurn = 1, ZoneStayTurn = 1, Keywords = [CardKeyword.Storm] };
        var target = unit with { InstanceId = Guid.NewGuid(), OwnerPlayerId = enemy, CurrentAttack = 2,
            CanAttack = false, Keywords = [] };
        var state = new GameStateDto { CurrentPlayerId = own, Players = [
            new() { PlayerId = own, LeaderCard = Leader(own, 20), Field = [unit] },
            new() { PlayerId = enemy, LeaderCard = Leader(enemy, 20), Field = [target] }] };
        var original = JsonSerializer.Serialize(state);
        var baseline = Plan(state, own, null)!;
        Check(Attack(baseline).TargetUid == state.Players[1].LeaderCard!.InstanceId, "fixture expected a close pressure choice");
        var calls = 0; var afterStates = 0;
        double PreferTrade(GameStateDto projected)
        {
            calls++;
            if (projected.Players[0].Field.Any(card => card.AttacksThisTurn > 0)) afterStates++;
            return projected.Players[1].Field.Count == 0 ? 1 : projected.Players[1].LeaderCard!.CurrentHealth < 20 ? -1 : 0;
        }
        var guided = Plan(state, own, PreferTrade)!;
        Check(calls > 2 && afterStates > 0 && Attack(guided).TargetUid == target.InstanceId,
            "local value estimates did not reach alternate search afterstates or affect a close decision");
        var invalid = Plan(state, own, _ => double.NaN)!;
        Check(Attack(invalid).TargetUid == Attack(baseline).TargetUid
            && Math.Abs(Field<double>(invalid, "Gain") - Field<double>(baseline, "Gain")) < 1e-9,
            "non-finite model prediction changed the baseline policy");
        Check(JsonSerializer.Serialize(state) == original, "search mutated the caller's board or counters");
        state.Players[1].LeaderCard = state.Players[1].LeaderCard! with { CurrentHealth = 3 };
        var lethal = Plan(state, own, PreferTrade)!;
        Check(Field<bool>(lethal, "ProjectsLethal") && Attack(lethal).TargetUid == state.Players[1].LeaderCard!.InstanceId,
            "learned preferences overrode a known lethal");
        state.Players[1].LeaderCard = state.Players[1].LeaderCard! with { CurrentHealth = 20 };
        state.Players[0].LeaderCard = state.Players[0].LeaderCard! with { CurrentHealth = 2 };
        var defend = Plan(state, own, projected => projected.Players[1].Field.Count == 0 ? -1 : 1)!;
        Check(Field<bool>(defend, "PreventsVisibleLethal") && Attack(defend).TargetUid == target.InstanceId,
            "learned preferences overrode basic lethal defense");
        state.Players[1].Field.Clear();
        state.Players[0].LeaderCard = state.Players[0].LeaderCard! with { CurrentHealth = 20 };
        state.Players[0].Field = [unit with { CurrentAttack = 1, CurrentHealth = 1, MaxHealth = 1, CanAttack = false, CanEvolve = true, ZoneStayTurn = 0 }];
        state.Players[0].CanEvolve = state.Players[0].CanSuperEvolve = true;
        state.Players[0].CurrentEP = state.Players[0].CurrentSEP = 2;
        var resource = Plan(state, own, projected => projected.Players[0].CurrentSEP < 2 ? 1 : -1);
        Check(resource == null || !Field<bool>(resource, "SpendsSep") || Field<double>(resource, "Gain") <= 0,
            "value preference alone promoted an unnecessary SEP expense");
        Check(unit.AttacksThisTurn == 0 && target.CurrentHealth == 3, "search mutated source cards");
        Console.WriteLine($"PASS bounded learned search: {calls} local fixture evaluations; alternate afterstates, lethal/defense/resource priorities retained; provider calls=0");
    }

    internal static void EndTurnValueCounters()
    {
        var own = Guid.NewGuid(); var enemy = Guid.NewGuid();
        var source = new CardInstanceDto { InstanceId = Guid.NewGuid(), OwnerPlayerId = own,
            Type = CardType.Follower, CurrentHealth = 3, MaxHealth = 3 };
        var target = source with { InstanceId = Guid.NewGuid(), OwnerPlayerId = enemy };
        var shield = target with { InstanceId = Guid.NewGuid(), CurrentHealth = 1, MaxHealth = 1, Keywords = [CardKeyword.Shield] };
        var leader = new CardInstanceDto { InstanceId = Guid.NewGuid(), OwnerPlayerId = enemy,
            Type = CardType.Leader, CurrentHealth = 20, MaxHealth = 20 };
        var state = new GameStateDto { CurrentPlayerId = own, Players = [
            new() { PlayerId = own, Field = [source] },
            new() { PlayerId = enemy, LeaderCard = leader, Field = [target, shield], BloodPool = 7, GraveyardCount = 9,
                Counters = new() { [PlayerState.BloodPoolCounterKey] = 7, [PlayerState.GraveyardCounterKey] = 9 } }] };
        var before = JsonSerializer.Serialize(state);
        var pending = Search.GetNestedType("PendingDamage", BindingFlags.NonPublic)!;
        var hits = Array.CreateInstance(pending, 2);
        hits.SetValue(Activator.CreateInstance(pending, [source.InstanceId, new[] { target.InstanceId, shield.InstanceId, leader.InstanceId }, 3, 0]), 0);
        hits.SetValue(Activator.CreateInstance(pending, [source.InstanceId, new[] { shield.InstanceId, leader.InstanceId }, 1, 0]), 1);
        var ending = Activator.CreateInstance(Search.GetNestedType("EndTurnForecast", BindingFlags.NonPublic)!, [hits, new HashSet<Guid>()]);
        var projected = (GameStateDto)Search.GetMethod("ProjectEndTurn", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state, own, ending])!;
        var side = projected.Players.Single(player => player.PlayerId == enemy);
        Check(side.LeaderCard!.CurrentHealth == 16 && side.Field.Count == 0 && side.BloodPool == 11 && side.GraveyardCount == 11
            && side.Counters[PlayerState.BloodPoolCounterKey] == 11 && side.Counters[PlayerState.GraveyardCounterKey] == 11,
            "end-turn afterstate omitted damage/death counters or damaged through Shield");
        var features = AiFeatures.Value(projected, own);
        Check(Math.Abs(features[45] - 1.1) < 1e-9 && Math.Abs(features[46] - 11 / 30d) < 1e-9,
            "learned afterstate did not receive projected blood/graveyard resources");
        Check(JsonSerializer.Serialize(state) == before, "forecast counter updates mutated the actual board");
        Console.WriteLine("PASS learned end-turn inputs: sequential Shield damage, blood/graveyard counters, fixed feature slots and original-state isolation; provider calls=0");
    }

    internal static void CapturedDeckAndArtifact()
    {
        var path = Path.Combine(Path.GetTempPath(), "sv-search-capture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        var file = Path.Combine(path, "model.json");
        AiNetwork Net(double bias) => new() { Format = 1, FeatureVersion = 3, SnapshotId = "local-fixture",
            W1 = new double[512 * 128], B1 = new double[128], W2 = new double[128], B2 = bias };
        AiModelArtifact Artifact(string version, double global, double deck) => new() { Version = version,
            Weights = new() { ValueModel = "value" },
            Specialists = new() { [AiLearningModel.DeckKey([11, 12])] = new() { ValueModel = "special" } },
            Models = new() { ["value"] = Net(global), ["special"] = Net(deck) } };
        var own = Guid.NewGuid(); var enemy = Guid.NewGuid();
        var game = new GameState("capture-test", new PlayerState(own) { InitialDeckDefinitionIds = [11, 12] }, new PlayerState(enemy), own);
        var state = new GameStateDto { CurrentPlayerId = own, Players = [new() { PlayerId = own }, new() { PlayerId = enemy }] };
        var capture = typeof(AiLearningModel).GetMethod("CaptureValueEvaluator", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Func<GameStateDto, double> Capture(AiLearningModel model, Guid actor) => (Func<GameStateDto, double>)capture.Invoke(model, [game, actor])!;
        try
        {
            File.WriteAllText(file, JsonSerializer.Serialize(Artifact("old", -.8, .8), AiLearningModel.Json));
            var model = new AiLearningModel(Proxy<IWebHostEnvironment>.Make((m, a) => null),
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AiModel:Path"] = file }).Build(),
                NullLogger<AiLearningModel>.Instance);
            var incumbent = model.Current!;
            var frozen = Capture(model, own); var old = frozen(state);
            Check(old > 0 && Capture(model, enemy)(state) < 0, "captured evaluator ignored deck specialist or actor");
            var candidate = Artifact("candidate", .5, -.5);
            model.SaveCandidateAsync(candidate, CancellationToken.None).GetAwaiter().GetResult();
            Check(Math.Abs(frozen(state) - old) < 1e-12 && Capture(model, own)(state) < 0,
                "mid-search model promotion changed an already captured evaluator");
            game.IsLocalAiTraining = true;
            var begin = typeof(AiLearningModel).GetMethod("BeginComparison", BindingFlags.Instance | BindingFlags.NonPublic)!;
            using var lease = (IDisposable)begin.Invoke(model, [game, own, candidate, incumbent])!;
            Check(Capture(model, own)(state) < 0 && Capture(model, enemy)(state) < 0,
                "frozen A/B artifact or per-deck network was not honored by search capture");
            Check(model.Current!.Version == "candidate", "capture changed the current model");
            Console.WriteLine("PASS captured value evaluator: correct deck specialist, actor, frozen artifact across promotion and comparison; provider calls=0");
        }
        finally { CleanupFixture(path, "sv-search-capture-"); }
    }
}
