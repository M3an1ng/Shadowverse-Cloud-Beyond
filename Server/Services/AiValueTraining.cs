namespace ShadowverseBeyond.Server.Services;

internal sealed record AiTrainingSample(int Episode, string DeckKey, double[] Features, double Target)
{
    public bool? AfterAction {get;init;}
}
public sealed record AiValueEpoch(int Epoch, double? TrainingLoss, double? ValidationLoss, double? TurnStartLoss, double? AfterActionLoss);
public sealed record AiValueValidation(string Scope, int TrainingGames, int ValidationGames,
    double? Before, double? After, bool Accepted, string? Reason = null)
{
    public AiValueEpoch[] Epochs {get;init;}=[];
    public int SelectedEpoch {get;init;}
}
internal sealed record AiValueTrainingResult(AiModelArtifact? Candidate, IReadOnlyList<AiValueValidation> Validations);

// Each deck is fitted from its own evaluator, with whole games held out of training.
// Validation error measures prediction accuracy, not match strength.
internal static class AiValueTraining
{
    internal static AiValueTrainingResult Fit(AiModelArtifact artifact, IReadOnlyList<AiTrainingSample> samples,
        IReadOnlySet<string> deckKeys, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (samples.Any(s => s.Episode < 0 || !deckKeys.Contains(s.DeckKey) || s.Features.Length != 512
            || s.Features.Any(v => !double.IsFinite(v)) || !double.IsFinite(s.Target) || Math.Abs(s.Target) > 1))
            throw new InvalidDataException("训练样本包含无效的牌组、特征或结果");
        var validations = new List<AiValueValidation>();
        var models = new Dictionary<string, AiNetwork>(artifact.Models);
        var specialists = new Dictionary<string, AiWeights>(artifact.Specialists);
        var version = "native-value-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N")[..6];
        var changed = false;
        AiWeights Train(string scope, AiWeights previousWeights, AiTrainingSample[] scoped)
        {
            token.ThrowIfCancellationRequested();
            var training = scoped.Where(s => s.Episode % 5 != 0).ToArray();
            var validation = scoped.Where(s => s.Episode % 5 == 0).ToArray();
            var trainGames = training.Select(s => s.Episode).Distinct().Count();
            var validationGames = validation.Select(s => s.Episode).Distinct().Count();
            if (trainGames < 4 || validationGames < 2)
            {
                validations.Add(new(scope, trainGames, validationGames, null, null, false, "有效训练或独立验证对局不足"));
                return previousWeights;
            }
            if (previousWeights.ValueModel is not string id || !artifact.Models.TryGetValue(id, out var previous)
                || previous.FeatureVersion != 3 || previous.Data != null)
            {
                validations.Add(new(scope, trainGames, validationGames, null, null, false, "缺少可训练的第三版局面网络"));
                return previousWeights;
            }
            previous.Validate();
            static AiNetwork Copy(AiNetwork source) => new() { Format = source.Format, FeatureVersion = source.FeatureVersion,
                SnapshotId = source.SnapshotId, Updates = source.Updates, W1 = (double[])source.W1.Clone(),
                W2 = (double[])source.W2.Clone(), B1 = (double[])source.B1.Clone(), B2 = source.B2 };
            var net = Copy(previous);
            AiNetwork? best = null;
            var bestLoss = double.PositiveInfinity;
            var bestEpoch = 0;
            double Loss(AiNetwork network) => validation.Average(s => Math.Pow(network.Predict(s.Features) - s.Target, 2));
            var before = Loss(previous);
            var ordered = training.OrderBy(s => AiFeatures.Hash(s.Episode + ":" + s.Features[1])).ToArray();
            double? Measure(AiNetwork network, AiTrainingSample[] rows)
            {
                if (rows.Length == 0) return null;
                var loss = rows.Average(sample => Math.Pow(network.Predict(sample.Features) - sample.Target, 2));
                return double.IsFinite(loss) ? loss : null;
            }
            var turnStarts = validation.Where(sample => sample.AfterAction == false).ToArray();
            var afterActions = validation.Where(sample => sample.AfterAction == true).ToArray();
            AiValueEpoch MeasureEpoch(int epoch) => new(epoch, Measure(net, training), Measure(net, validation),
                Measure(net, turnStarts), Measure(net, afterActions));
            var epochs = new List<AiValueEpoch> {MeasureEpoch(0)};
            for (var epoch = 0; epoch < 3; epoch++)
            {
              foreach (var sample in ordered)
              {
                token.ThrowIfCancellationRequested();
                var x = sample.Features; var h = new double[128]; var output = net.B2;
                for (var j = 0; j < 128; j++)
                {
                    var sum = net.B1[j];
                    for (var i = 0; i < 512; i++) sum += net.W1[j * 512 + i] * x[i];
                    h[j] = Math.Tanh(sum); output += h[j] * net.W2[j];
                }
                var prediction = Math.Tanh(output);
                var delta = Math.Clamp((prediction - sample.Target) * (1 - prediction * prediction), -1, 1);
                const double rate = .002;
                for (var j = 0; j < 128; j++)
                {
                    var grad = delta * net.W2[j] * (1 - h[j] * h[j]);
                    net.W2[j] -= rate * delta * h[j]; net.B1[j] -= rate * grad;
                    for (var i = 0; i < 512; i++) net.W1[j * 512 + i] -= rate * (grad * x[i] + .0001 * net.W1[j * 512 + i]);
                }
                net.B2 -= rate * delta; net.Updates++;
              }
              var measured = MeasureEpoch(epoch + 1);
              epochs.Add(measured);
              // These whole-game holdouts select a checkpoint; only the separate match gate can promote it.
              // Later SGD passes must not overwrite an earlier, better evaluator.
              if (measured.ValidationLoss is double loss && loss < bestLoss)
              {
                  best = Copy(net); bestLoss = loss; bestEpoch = epoch + 1;
              }
            }
            var after = bestLoss;
            var accepted = best != null && double.IsFinite(after) && after < before - 1e-5;
            validations.Add(new(scope, trainGames, validationGames, before, double.IsFinite(after) ? after : null,
                accepted, accepted ? null : "留出对局误差未改善，保留原网络")
                {Epochs=epochs.ToArray(), SelectedEpoch=accepted?bestEpoch:0});
            if (!accepted) return previousWeights;
            net = best!;
            net.Validate();
            var key = version + "-" + scope;
            models.Add(key, net); changed = true;
            return new() { Board = previousWeights.Board, Face = previousWeights.Face, Survival = previousWeights.Survival,
                Cards = previousWeights.Cards, Resource = previousWeights.Resource, Tempo = previousWeights.Tempo,
                Utility = previousWeights.Utility, ValueModel = key, ValueTeacher = previousWeights.ValueTeacher,
                ActionPolicy = previousWeights.ActionPolicy };
        }
        var global = Train("global", artifact.Weights, samples.ToArray());
        foreach (var key in deckKeys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var previous = artifact.Specialists.GetValueOrDefault(key) ?? artifact.Weights;
            // Pin a rejected deck to its prior evaluator even when the global evaluator improves.
            specialists[key] = Train(key, previous, samples.Where(s => s.DeckKey == key).ToArray());
        }
        if (!changed) return new(null, validations);
        var referenced = specialists.Values.Prepend(global).SelectMany(w => new[] { w.ValueModel, w.ValueTeacher, w.ActionPolicy })
            .Where(id => id != null).ToHashSet();
        return new(new() { Version = version, Weights = global, Specialists = specialists,
            Models = models.Where(e => referenced.Contains(e.Key)).ToDictionary(e => e.Key, e => e.Value) }, validations);
    }
}
