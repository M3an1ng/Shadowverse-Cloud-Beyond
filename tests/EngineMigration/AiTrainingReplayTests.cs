using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using ShadowverseBeyond.Server.Services;

internal static class AiTrainingReplayTests
{
    private static readonly System.Reflection.Assembly Assembly = typeof(AiLearningModel).Assembly;
    private static readonly Type Store = Assembly.GetType("ShadowverseBeyond.Server.Services.AiTrainingReplayStore")!;
    private static readonly Type SampleType = Assembly.GetType("ShadowverseBeyond.Server.Services.AiTrainingSample")!;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static object Invoke(Type type, string method, params object?[] args)
    {
        try { return type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!; }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static object Property(object value, string name) => value.GetType().GetProperty(name)!.GetValue(value)!;
    private static string SourceHash(AiModelArtifact model) => (string)typeof(AiModelArtifact)
        .GetProperty("SourceHash", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(model)!;
    private static AiModelArtifact Artifact() => new() { Version = "replay-fixture", Weights = new() { ValueModel = "v" },
        Models = new() { ["v"] = new() { Format = 1, FeatureVersion = 3, SnapshotId = "fixture",
            W1 = new double[512 * 128], W2 = new double[128], B1 = new double[128] } } };
    private static Array Data(string deck)
    {
        var rows = Array.CreateInstance(SampleType, 20);
        for (var episode = 0; episode < 10; episode++)
            for (var kind = 0; kind < 2; kind++)
            {
                var x = new double[512]; x[0] = 1;
                var row = Activator.CreateInstance(SampleType, episode, deck, x, episode % 5 == 0 ? -1d : 1d)!;
                SampleType.GetProperty("AfterAction")!.SetValue(row, kind == 1);
                rows.SetValue(row, episode * 2 + kind);
            }
        return rows;
    }
    private static void Cleanup(string path)
    {
        var absolute = Path.GetFullPath(path);
        var temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (!absolute.StartsWith(temporary, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(absolute).StartsWith("sv-ai-replay-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected fixture cleanup target");
        if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
    }

    internal static void RoundTripAndIdentity()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sv-ai-replay-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "model.json"); File.WriteAllText(file, JsonSerializer.Serialize(Artifact(), AiLearningModel.Json));
            var model = AiLearningModel.Load(file);
            Check(SourceHash(model) == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant(),
                "model identity was not computed from the exact loaded bytes");
            Check(!JsonSerializer.Serialize(model, AiLearningModel.Json).Contains("sourceHash", StringComparison.OrdinalIgnoreCase),
                "local source-file identity changed the portable model format");
            var key = AiLearningModel.DeckKey([1, 2]); var rows = Data(key);
            var coverage = new AiTrainingCoverage(10, 10, 10, 10, 10, 10) { ActionKinds = new() { ["PlayCard"] = 10 } };
            var rules = new AiDefinitionStamp(new('a', 64), new('b', 64), 2, 0, 0, true, []);
            var replay = Invoke(Store, "Create", model, rules, rows, coverage);
            ((double[])Property(rows.GetValue(0)!, "Features"))[0] = 777;
            coverage.ActionKinds["PlayCard"] = 777;
            var path = Path.Combine(dir, "replay.json");
            ((Task)Invoke(Store, "SaveAsync", path, replay, CancellationToken.None)).GetAwaiter().GetResult();
            var loaded = Invoke(Store, "Load", path, model);
            var samples = ((IEnumerable)Property(loaded, "Samples")).Cast<object>().ToArray();
            Check(samples.Length == 20 && samples.All(row => ((double[])Property(row, "Features"))[0] == 1)
                && samples.Count(row => (bool?)row.GetType().GetProperty("AfterAction")!.GetValue(row) == true) == 10,
                "replay lost strata or shared mutable caller samples");
            var disk = File.ReadAllText(path);
            Check(new[] { "apiKey", "password", "email", "userId", "\"w1\"", "\"w2\"" }
                .All(value => !disk.Contains(value, StringComparison.OrdinalIgnoreCase)), "numeric replay included credentials, accounts or model weights");
            var oldBytes = File.ReadAllBytes(path);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel(); var cancelled = false;
                try { ((Task)Invoke(Store, "SaveAsync", path, loaded, cts.Token)).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled && File.ReadAllBytes(path).SequenceEqual(oldBytes) && !Directory.GetFiles(dir, "*.tmp").Any(),
                    "cancelled save replaced the last complete replay or leaked a temporary file");
            }
            model.Models["v"].B2 = .1;
            var other = Path.Combine(dir, "other-model.json"); File.WriteAllText(other, JsonSerializer.Serialize(model, AiLearningModel.Json));
            var rejected = false;
            try { Invoke(Store, "Load", path, AiLearningModel.Load(other)); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "a same-version model with different bytes accepted the replay");
            var json = System.Text.Json.Nodes.JsonNode.Parse(disk)!; json["samples"]![0]!["features"] = new System.Text.Json.Nodes.JsonArray(1);
            File.WriteAllText(path, json.ToJsonString()); rejected = false;
            try { Invoke(Store, "Load", path, AiLearningModel.Load(file)); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "malformed features entered replay fitting");
            Console.WriteLine("PASS numeric replay: immutable roundtrip, exact model-file hash, stage metadata, cancellation and wrong-model rejection; no external provider");
        }
        finally { Cleanup(dir); }
    }

    internal static void EpochDiagnostics()
    {
        var artifact = Artifact(); var key = AiLearningModel.DeckKey([1, 2]); var rows = Data(key);
        var before = JsonSerializer.Serialize(artifact, AiLearningModel.Json);
        object Fit() => Invoke(Assembly.GetType("ShadowverseBeyond.Server.Services.AiValueTraining")!, "Fit", artifact, rows,
            new HashSet<string> { key }, CancellationToken.None);
        var result = Fit(); var again = Fit();
        Check(Property(result, "Validations") is IReadOnlyList<AiValueValidation>, "missing validation diagnostics");
        var validations = (IReadOnlyList<AiValueValidation>)Property(result, "Validations");
        Check(validations.All(value => !value.Accepted && value.Epochs.Length == 4 && value.Epochs[0].Epoch == 0
            && value.Epochs[3].TrainingLoss < value.Epochs[0].TrainingLoss
            && value.Epochs[3].ValidationLoss > value.Epochs[0].ValidationLoss
            && value.Epochs.All(epoch => epoch.TurnStartLoss.HasValue && epoch.AfterActionLoss.HasValue)),
            "diagnostics failed to distinguish learning the training labels from held-out regression");
        Check(JsonSerializer.Serialize(Property(result, "Validations"), Json) == JsonSerializer.Serialize(Property(again, "Validations"), Json)
            && JsonSerializer.Serialize(artifact, AiLearningModel.Json) == before, "repeating fitting changed its input or numeric diagnostics");
        Console.WriteLine("PASS epoch diagnostics: repeatable same-data fitting, separated turn-start/action holdouts and explicit overfit trajectory; no external provider");
    }

    internal static void BestCheckpoint()
    {
        var model = Artifact(); var deck = AiLearningModel.DeckKey([1, 2]);
        var rows = Array.CreateInstance(SampleType, 10);
        for (var episode = 0; episode < 10; episode++)
        {
            var x = new double[512]; x[0] = 1;
            var row = Activator.CreateInstance(SampleType, episode, deck, x, episode % 5 == 0 ? .02d : 1d)!;
            SampleType.GetProperty("AfterAction")!.SetValue(row, false); rows.SetValue(row, episode);
        }
        var original = JsonSerializer.Serialize(model, AiLearningModel.Json);
        var result = Invoke(Assembly.GetType("ShadowverseBeyond.Server.Services.AiValueTraining")!, "Fit", model, rows,
            new HashSet<string> {deck}, CancellationToken.None);
        var validations = (IReadOnlyList<AiValueValidation>)Property(result, "Validations");
        var candidate = (AiModelArtifact)Property(result, "Candidate");
        Check(validations.All(value => value.Accepted && value.SelectedEpoch == 1 && value.After < value.Before
            && value.Epochs[3].ValidationLoss > value.Before
            && value.After == value.Epochs[value.SelectedEpoch].ValidationLoss),
            "the last, worse SGD pass replaced a better held-out checkpoint");
        foreach (var weights in candidate.Specialists.Values.Prepend(candidate.Weights))
        {
            var net = candidate.Models[weights.ValueModel!]; var prediction = net.Predict(new double[512]);
            var scope = ReferenceEquals(weights, candidate.Weights) ? "global" : deck;
            var picked = validations.Single(value => value.Scope == scope);
            Check(Math.Abs(Math.Pow(prediction - .02, 2) - picked.After!.Value) < 1e-12
                && net.Updates == 8, "candidate weights or update count did not match the selected epoch");
        }
        Check(JsonSerializer.Serialize(model, AiLearningModel.Json) == original, "checkpoint selection mutated live weights");
        Console.WriteLine("PASS checkpoint selection: restores the best whole-game holdout epoch including exact weights/updates; incumbent immutable; match gate still required");
    }

    internal static void Diagnose(string modelPath, string replayPath, string output)
    {
        var model = AiLearningModel.Load(modelPath);
        var replay = Invoke(Store, "Load", replayPath, model);
        var result = Invoke(Assembly.GetType("ShadowverseBeyond.Server.Services.AiValueTraining")!, "Fit", model,
            Property(replay, "Samples"), ((string[])Property(replay, "DeckKeys")).ToHashSet(), CancellationToken.None);
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            providerCalls = 0, modelVersion = model.Version, modelHash = SourceHash(model), recordedRules = Property(replay, "Rules"),
            samples = ((Array)Property(replay, "Samples")).Length, coverage = Property(replay, "Coverage"),
            validations = Property(result, "Validations"), candidateAvailable = result.GetType().GetProperty("Candidate")!.GetValue(result) != null,
            scope = "Numeric replay under archived rules; no game execution, model promotion or strength evidence"
        }, new JsonSerializerOptions(Json) { WriteIndented = true }));
        Console.WriteLine("Offline replay diagnostic written; provider calls=0; no model weights saved or replaced");
    }
}
