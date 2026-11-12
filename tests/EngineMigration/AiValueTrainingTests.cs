using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using ShadowverseBeyond.Server.Services;

internal static class AiValueTrainingTests
{
    private static void Check(bool test, string message) { if (!test) throw new Exception(message); }
    private static AiNetwork Net(double bias) => new() { Format = 1, FeatureVersion = 3, SnapshotId = "offline-training-fixture",
        W1 = new double[512 * 128], B1 = new double[128], W2 = new double[128], B2 = bias };
    private static double[] X() { var x = new double[512]; x[0] = 1; return x; }
    private static (AiModelArtifact? Candidate, AiValueValidation[] Validations) Fit(AiModelArtifact artifact,
        (int Episode, string Key, double[] Features, double Target)[] samples, CancellationToken token = default)
    {
        var assembly = typeof(AiLearningModel).Assembly;
        var sampleType = assembly.GetType("ShadowverseBeyond.Server.Services.AiTrainingSample")!;
        var array = Array.CreateInstance(sampleType, samples.Length);
        for (var i = 0; i < samples.Length; i++)
            array.SetValue(Activator.CreateInstance(sampleType, samples[i].Episode, samples[i].Key, samples[i].Features, samples[i].Target), i);
        object result;
        try { result = assembly.GetType("ShadowverseBeyond.Server.Services.AiValueTraining")!
            .GetMethod("Fit", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null,
                [artifact, array, samples.Select(s => s.Key).ToHashSet(), token])!; }
        catch (TargetInvocationException e) when (e.InnerException != null)
        { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        return ((AiModelArtifact?)result.GetType().GetProperty("Candidate")!.GetValue(result),
            ((IReadOnlyList<AiValueValidation>)result.GetType().GetProperty("Validations")!.GetValue(result)!).ToArray());
    }
    private static AiModelArtifact Artifact() => new() { Version = "prior", Weights = new() { ValueModel = "global" },
        Specialists = new() { ["a"] = new() { ValueModel = "a", Face = 1.7, ActionPolicy = "policy" },
            ["b"] = new() { ValueModel = "b" }, ["untrained"] = new() { ValueModel = "untouched" } },
        Models = new() { ["global"] = Net(0), ["a"] = Net(.6), ["b"] = Net(-.6),
            ["untouched"] = Net(.2), ["policy"] = new() { Format = 1, FeatureVersion = 1,
                W1 = new double[256 * 64], B1 = new double[64], W2 = new double[64] } } };

    internal static void IndependentDecks()
    {
        var artifact = Artifact(); var original = JsonSerializer.Serialize(artifact, AiLearningModel.Json);
        var samples = Enumerable.Range(0, 10).SelectMany(episode => new[] {
            (episode, "a", X(), 1d), (episode, "b", X(), -1d) }).ToArray();
        var result = Fit(artifact, samples); var next = result.Candidate ?? throw new Exception("specialists did not learn");
        var a = next.Models[next.Specialists["a"].ValueModel!]; var b = next.Models[next.Specialists["b"].ValueModel!];
        Check(next.Specialists["a"].ValueModel != next.Specialists["b"].ValueModel,
            "opposite deck outcomes collapsed into one evaluator");
        Check(a.Predict(X()) > artifact.Models["a"].Predict(X()) && b.Predict(X()) < artifact.Models["b"].Predict(X()),
            "a specialist failed to learn from its own prior and outcomes");
        Check(a.B2 > .6 && b.B2 < -.6, "specialist training restarted from the shared evaluator");
        Check(next.Specialists["a"].Face == 1.7 && next.Specialists["a"].ActionPolicy == "policy",
            "value training replaced policy or heuristic settings");
        Check(next.Specialists["untrained"].ValueModel == "untouched"
            && ReferenceEquals(next.Models["untouched"], artifact.Models["untouched"]), "untrained specialist changed");
        Check(JsonSerializer.Serialize(artifact, AiLearningModel.Json) == original, "fitting mutated active weights");
        Check(result.Validations.Count(v => v.Accepted) == 2 && result.Validations.All(v => v.TrainingGames == 8 && v.ValidationGames == 2),
            "whole-game holdouts or per-deck validation were lost");
        foreach (var weights in next.Specialists.Values.Append(next.Weights))
            Check(weights.ValueModel != null && next.Models.ContainsKey(weights.ValueModel), "candidate lost a referenced evaluator");
    }

    internal static void RejectRegressions()
    {
        var artifact = Artifact(); artifact.Models["a"].B2 = 0;
        var samples = Enumerable.Range(0, 10).Select(e => (e, "a", X(), e % 5 == 0 ? -1d : 1d)).ToArray();
        var result = Fit(artifact, samples);
        Check(result.Candidate == null && result.Validations.All(v => !v.Accepted && v.After > v.Before),
            "better training predictions replaced a worse held-out evaluator");
        result = Fit(artifact, samples.Where(s => s.e != 5).ToArray());
        Check(result.Candidate == null && result.Validations.All(v => v.ValidationGames == 1 && v.Before == null),
            "insufficient independent games passed validation");
        artifact = Artifact();
        var mixed = Enumerable.Range(0, 10).SelectMany(e => new[] { (e, "new", X(), 0d), (e, "b", X(), -1d) }).ToArray();
        result = Fit(artifact, mixed);
        var next = result.Candidate ?? throw new Exception("mixed data did not improve any evaluator");
        Check(next.Weights.ValueModel != "global" && next.Specialists["new"].ValueModel == "global",
            "a rejected new specialist silently inherited an unvalidated global change");
        Check(next.Models[next.Specialists["new"].ValueModel!].Predict(X()) == 0,
            "a held-out rejection changed the deck fallback");
    }

    internal static void RejectInvalidSamples()
    {
        var invalid = X(); invalid[50] = double.NaN; var rejected = false;
        try { Fit(Artifact(), [(0, "a", invalid, 1d)]); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "invalid samples entered neural updates");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); rejected = false;
        try { Fit(Artifact(), [(0, "a", X(), 1d)], cancellation.Token); } catch (OperationCanceledException) { rejected = true; }
        Check(rejected, "cancelled training still fitted a model");
    }
}
