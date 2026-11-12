using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiTrainingPositionsTests
{
    private static readonly Type TraceType = typeof(AiLearningModel).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTrainingPositions")!;
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static object Invoke(object target, string method, params object?[] args)
    {
        try { return TraceType.GetMethod(method, Hidden)!.Invoke(target, args)!; }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static object Trace(int episode, int seed, Guid first, Guid second) =>
        Activator.CreateInstance(TraceType, Hidden, null, [episode, seed, first, second], null)!;
    private static AiTrainingCoverage Coverage(object trace) => (AiTrainingCoverage)TraceType.GetProperty("Coverage", Hidden)!.GetValue(trace)!;
    private sealed record Sample(int Episode, string Deck, double[] X, double Target);
    private static Sample[] Samples(object trace, Dictionary<Guid, string> decks, Guid? winner) =>
        ((IEnumerable)Invoke(trace, "Samples", decks, winner)).Cast<object>().Select(item =>
        {
            object P(string name) => item.GetType().GetProperty(name)!.GetValue(item)!;
            return new Sample((int)P("Episode"), (string)P("DeckKey"), (double[])P("Features"), (double)P("Target"));
        }).ToArray();
    private static double[] X(int index, bool after) { var x = new double[512]; x[0] = 1; x[1] = index; x[2] = after ? 2 : 1; return x; }

    internal static void BoundedStrataAndLabels()
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var trace = Trace(7, 82431, first, second); var replay = Trace(7, 82431, first, second);
        var kinds = new[] { "PlayCard", "PerformAttack", "PerformEvolve", "PerformSuperEvolve", "RequestAct", "RequestFusion", "PerformPpBoost", "EndTurn" };
        foreach (var current in new[] { trace, replay })
            for (var i = 0; i < 5000; i++)
                foreach (var actor in new[] { first, second })
                {
                    if (i % 100 == 0) Invoke(current, "Record", actor, false, X(i, false), null);
                    var x = X(i, true); Invoke(current, "Record", actor, true, x, kinds[i % kinds.Length]);
                    x[0] = -999; x[1] = -999;
                }
        var before = Coverage(trace);
        Check(before.ObservedTurnStarts == 100 && before.ObservedAfterActions == 10000
            && before.RetainedTurnStarts <= 60 && before.RetainedAfterActions <= 60,
            "long games grew retained memory without bounds or lost observation counts");
        var keys = new Dictionary<Guid, string> { [first] = "a", [second] = "b" };
        var samples = Samples(trace, keys, first);
        Check(samples.Length == 32 && samples.All(sample => sample.Episode == 7 && sample.X.Length == 512 && sample.X[0] == 1),
            "samples exceeded the per-player budget, mixed episodes, or retained mutable caller arrays");
        foreach (var deck in new[] { "a", "b" })
        {
            var part = samples.Where(sample => sample.Deck == deck).ToArray();
            Check(part.Length == 16 && part.Count(sample => sample.X[2] == 1) == 8 && part.Count(sample => sample.X[2] == 2) == 8,
                "a long combo crowded out turn starts or mixed player samples");
            Check(part.All(sample => sample.Target == (deck == "a" ? 1 : -1)), "winner labels were swapped between players");
            Check(part.Any(sample => sample.X[2] == 1 && sample.X[1] == 0)
                && part.Any(sample => sample.X[2] == 1 && sample.X[1] == 4900)
                && part.Any(sample => sample.X[2] == 2 && sample.X[1] == 0)
                && part.Any(sample => sample.X[2] == 2 && sample.X[1] == 4999), "opening or latest positions were lost");
        }
        var expected = Samples(replay, keys, first);
        Check(samples.Zip(expected).All(pair => pair.First.Deck == pair.Second.Deck && pair.First.X.SequenceEqual(pair.Second.X)),
            "sampling used global engine randomness or was not reproducible for one trace");
        samples[0].X[0] = 123;
        Check(Samples(trace, keys, first).All(sample => sample.X[0] == 1), "training modified retained observations");
        var coverage = Coverage(trace);
        Check(coverage.SamplesTurnStarts == 16 && coverage.SamplesAfterActions == 16 && coverage.ActionKinds.Count == 8
            && coverage.ActionKinds.Values.Sum() == 10000, "sampling coverage disagreed with actual selected strata or action kinds");
        Check(Samples(trace, keys, Guid.Empty).All(sample => sample.Target == 0), "draws were labelled as losses");
        Console.WriteLine("PASS bounded real-position sampling: 10100 observations, at most 120 retained vectors, 32 selected with balanced strata and correct outcome labels; provider calls=0");
    }

    internal static void SparseAndInvalidObservations()
    {
        var own = Guid.NewGuid(); var enemy = Guid.NewGuid();
        var trace = Trace(0, 1, own, enemy);
        for (var i = 0; i < 3; i++) Invoke(trace, "Record", own, false, X(i, false), null);
        for (var i = 0; i < 26; i++) Invoke(trace, "Record", own, true, X(i, true), "PlayCard");
        var samples = Samples(trace, new() { [own] = "a", [enemy] = "b" }, null);
        Check(samples.Length == 16 && samples.Count(sample => sample.X[2] == 1) == 3 && samples.All(sample => sample.Target == 0),
            "unused stratum quota was not filled without inventing opponent positions");
        var before = Coverage(trace);
        void Reject(object?[] args)
        {
            var rejected = false;
            try { Invoke(trace, "Record", args); } catch (Exception error) when (error is ArgumentException or InvalidDataException) { rejected = true; }
            Check(rejected, "invalid observation was accepted");
        }
        Reject([Guid.NewGuid(), true, X(0, true), "PlayCard"]);
        Reject([own, true, new double[1], "PlayCard"]);
        var bad = X(0, true); bad[3] = double.NaN;
        Reject([own, true, bad, "PlayCard"]);
        Reject([own, true, X(0, true), "not-a-committed-game-action"]);
        var after = Coverage(trace);
        Check(before.ObservedTurnStarts == after.ObservedTurnStarts && before.ObservedAfterActions == after.ObservedAfterActions,
            "rejected data changed observation counts");
    }

    internal static void SuccessfulCompletionAndLeases()
    {
        var own = Guid.NewGuid(); var enemy = Guid.NewGuid();
        var game = new GameState("sampling-hook", new PlayerState(own), new PlayerState(enemy), own);
        var trace = Trace(0, 71, own, enemy); var pp = 10;
        GameStateDto Snapshot(Guid actor) => new() { CurrentPlayerId = own, Players = [
            new() { PlayerId = own, CurrentPP = pp }, new() { PlayerId = enemy }] };
        var rejected = false;
        try { Invoke(trace, "Attach", game, (Func<Guid, GameStateDto>)Snapshot); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "normal live games accepted a training observer");
        game.IsLocalAiTraining = true;
        var lease = (IDisposable)Invoke(trace, "Attach", game, (Func<Guid, GameStateDto>)Snapshot);
        rejected = false;
        try { Invoke(trace, "Attach", game, (Func<Guid, GameStateDto>)Snapshot); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "a second observer replaced the first");
        var admit = true;
        var games = Proxy<IGameService>.Make((method, args) => method.Name == "EnqueuePlayerActionAsync" ? Task.FromResult(admit) : null);
        using var services = new ServiceCollection().BuildServiceProvider();
        var executor = new AiTurnExecutor(games, Proxy<IAiService>.Make((m, a) => null), NullLogger<AiTurnExecutor>.Instance,
            Proxy<IMapper>.Make((m, a) => null), services);
        var submit = typeof(AiTurnExecutor).GetMethod("SubmitAiActionAsync", Hidden)!;
        Task<PlayerActionCompletion?> Submit(PlayerActionWrapper action, CancellationToken token = default) =>
            (Task<PlayerActionCompletion?>)submit.Invoke(executor, [game, action, null, token, null, null, null, null, null, null])!;
        var kinds = new[] { "PlayCard", "PerformAttack", "PerformEvolve", "PerformSuperEvolve", "RequestAct", "RequestFusion", "PerformPpBoost", "EndTurn" };
        for (var i = 0; i < kinds.Length; i++)
        {
            var action = new PlayerActionWrapper(kinds[i], own, new { });
            var pending = Submit(action);
            Check(!pending.IsCompleted && Coverage(trace).ObservedAfterActions == i, "proposal or admission was sampled before settlement");
            pp--; action.MarkCompleted(); pending.GetAwaiter().GetResult();
            Check(Coverage(trace).ObservedAfterActions == i + 1, "successful settled action was not observed exactly once");
        }
        var selected = Samples(trace, new() { [own] = "a", [enemy] = "b" }, own);
        Check(selected.Any(sample => Math.Abs(sample.X[6] - .9) < 1e-9) && selected.Any(sample => Math.Abs(sample.X[6] - .2) < 1e-9),
            "observer captured the proposal's resource state instead of the settled state");
        foreach (var status in new[] { "reject", "cancel", "fail" })
        {
            var action = new PlayerActionWrapper("PlayCard", own, new { }); var pending = Submit(action);
            if (status == "reject") action.MarkRejected("fixture"); else if (status == "cancel") action.MarkCancelled("fixture"); else action.MarkFailed("fixture");
            pending.GetAwaiter().GetResult();
        }
        admit = false; Check(Submit(new("PlayCard", own, new { })).GetAwaiter().GetResult() == null, "unadmitted action unexpectedly completed");
        Check(Coverage(trace).ObservedAfterActions == 8, "rejected, cancelled, failed or unadmitted actions entered training");
        admit = true; using (var cts = new CancellationTokenSource())
        {
            var pending = Submit(new("PlayCard", own, new { }), cts.Token); cts.Cancel();
            try { pending.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        }
        Check(Coverage(trace).ObservedAfterActions == 8, "cancelled waiting entered training");
        lease.Dispose();
        var next = Trace(1, 71, own, enemy);
        using var newer = (IDisposable)Invoke(next, "Attach", game, (Func<Guid, GameStateDto>)Snapshot);
        lease.Dispose();
        var completed = new PlayerActionWrapper("PlayCard", own, new { }); completed.MarkCompleted(); Submit(completed).GetAwaiter().GetResult();
        Check(Coverage(next).ObservedAfterActions == 1 && Coverage(trace).ObservedAfterActions == 8, "old lease disposal removed the new observer");
        game.IsLocalAiTraining = false;
        completed = new("PlayCard", own, new { }); completed.MarkCompleted(); Submit(completed).GetAwaiter().GetResult();
        Check(Coverage(next).ObservedAfterActions == 1, "observation leaked outside a local training game");
        Console.WriteLine("PASS settled action hook: eight action kinds, post-completion resources, rejection/cancellation isolation and exact lease cleanup; mock admission/completion, no native effect claim; provider calls=0");
    }
}
