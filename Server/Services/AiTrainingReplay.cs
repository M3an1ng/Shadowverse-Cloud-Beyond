using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Services;

// Local numeric training data only: no account identifiers, card text, credentials or model weights.
internal sealed record AiTrainingReplay(int Schema, int FeatureVersion, string ModelVersion, string ModelHash,
    AiDefinitionStamp Rules, string[] DeckKeys, AiTrainingCoverage Coverage, AiTrainingSample[] Samples);

internal static class AiTrainingReplayStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal const long MaxFileBytes = 100 * 1024 * 1024;
    private static bool Digest(string? text) => text is { Length: 64 } && text.All(Uri.IsHexDigit);

    internal static AiTrainingReplay Create(AiModelArtifact artifact, AiDefinitionStamp rules,
        IReadOnlyList<AiTrainingSample> samples, AiTrainingCoverage coverage)
    {
        if (!Digest(artifact.SourceHash)) throw new InvalidDataException("缺少原始模型文件身份，无法保存可复现训练数据");
        if (samples.Any(sample => sample == null)) throw new InvalidDataException("训练回放包含空局面");
        var replay = new AiTrainingReplay(1, 3, artifact.Version, artifact.SourceHash!, rules,
            samples.Select(sample => sample.DeckKey).Distinct().Order(StringComparer.Ordinal).ToArray(), coverage, samples.ToArray());
        Validate(replay);
        return replay with
        {
            Coverage = coverage with {ActionKinds = new(coverage.ActionKinds, StringComparer.Ordinal)},
            Samples = samples.Select(sample => sample with { Features = (double[])sample.Features.Clone() }).ToArray()
        };
    }

    private static void Validate(AiTrainingReplay replay)
    {
        if (replay.Schema != 1 || replay.FeatureVersion != 3 || string.IsNullOrWhiteSpace(replay.ModelVersion)
            || !Digest(replay.ModelHash) || replay.Rules?.Available != true || !Digest(replay.Rules.Fingerprint)
            || !Digest(replay.Rules.EngineFingerprint) || replay.DeckKeys is not { Length: >= 1 and <= 2 }
            || replay.DeckKeys.Any(key => !Digest(key)) || replay.DeckKeys.Distinct().Count() != replay.DeckKeys.Length
            || replay.Samples is not { Length: >= 1 and <= 32000 } || replay.Coverage == null)
            throw new InvalidDataException("训练回放格式或身份无效");
        var keys = replay.DeckKeys.ToHashSet(StringComparer.Ordinal);
        if (replay.Samples.Any(sample => sample == null || sample.Episode is < 0 or >= 1000
            || !keys.Contains(sample.DeckKey) || sample.Features is not { Length: 512 }
            || sample.Features.Any(value => !double.IsFinite(value)) || !double.IsFinite(sample.Target) || Math.Abs(sample.Target) > 1))
            throw new InvalidDataException("训练回放包含无效局面、结果或牌组");
        var coverage = replay.Coverage;
        var counts = new long[] {coverage.ObservedTurnStarts, coverage.ObservedAfterActions, coverage.RetainedTurnStarts,
            coverage.RetainedAfterActions, coverage.SamplesTurnStarts, coverage.SamplesAfterActions};
        if (counts.Any(value => value < 0) || coverage.ActionKinds == null || coverage.ActionKinds.Count > 8
            || coverage.ActionKinds.Any(pair => pair.Value < 0 || pair.Key.Length > 40)
            || coverage.ActionKinds.Values.Sum() != coverage.ObservedAfterActions
            || coverage.SamplesTurnStarts != replay.Samples.Count(sample => sample.AfterAction == false)
            || coverage.SamplesAfterActions != replay.Samples.Count(sample => sample.AfterAction == true)
            || coverage.SamplesTurnStarts > coverage.RetainedTurnStarts || coverage.RetainedTurnStarts > coverage.ObservedTurnStarts
            || coverage.SamplesAfterActions > coverage.RetainedAfterActions || coverage.RetainedAfterActions > coverage.ObservedAfterActions
            || replay.Samples.GroupBy(sample => sample.Episode).Any(group => group.Count() > 32))
            throw new InvalidDataException("训练回放覆盖统计或每局样本数不一致");
    }

    internal static async Task SaveAsync(string path, AiTrainingReplay replay, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Validate(replay);
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await JsonSerializer.SerializeAsync(file, replay, Json, token);
            if (new FileInfo(temporary).Length > MaxFileBytes) throw new InvalidDataException("训练回放超过大小限制");
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static AiTrainingReplay Load(string path, AiModelArtifact artifact)
    {
        using var file = File.OpenRead(path);
        if (file.Length > MaxFileBytes) throw new InvalidDataException("训练回放超过大小限制");
        AiTrainingReplay replay;
        try { replay = JsonSerializer.Deserialize<AiTrainingReplay>(file, Json) ?? throw new InvalidDataException("训练回放为空"); }
        catch (JsonException error) { throw new InvalidDataException("训练回放JSON无效", error); }
        Validate(replay);
        if (replay.ModelVersion != artifact.Version || !string.Equals(replay.ModelHash, artifact.SourceHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("训练回放与模型文件不匹配，未进行拟合");
        // Numeric fitting may be repeated for these archived rules; this is not proof of current game compatibility.
        return replay;
    }
}
