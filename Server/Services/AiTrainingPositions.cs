using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Services;

public sealed record AiTrainingCoverage(long ObservedTurnStarts = 0, long ObservedAfterActions = 0,
    int RetainedTurnStarts = 0, int RetainedAfterActions = 0, int SamplesTurnStarts = 0, int SamplesAfterActions = 0)
{
    public Dictionary<string, long> ActionKinds { get; init; } = new(StringComparer.Ordinal);
    internal AiTrainingCoverage Merge(AiTrainingCoverage other) => new(
        ObservedTurnStarts + other.ObservedTurnStarts, ObservedAfterActions + other.ObservedAfterActions,
        RetainedTurnStarts + other.RetainedTurnStarts, RetainedAfterActions + other.RetainedAfterActions,
        SamplesTurnStarts + other.SamplesTurnStarts, SamplesAfterActions + other.SamplesAfterActions)
    {
        ActionKinds = ActionKinds.Keys.Concat(other.ActionKinds.Keys).Distinct(StringComparer.Ordinal)
            .ToDictionary(key => key, key => ActionKinds.GetValueOrDefault(key) + other.ActionKinds.GetValueOrDefault(key), StringComparer.Ordinal)
    };
}

// Bounded observations of real settled positions, not projected actions or model proposals.
// Two strata per player: retain openings/recent positions plus an independent reservoir of middle positions.
internal sealed class AiTrainingPositions
{
    private sealed record Position(long Order, bool AfterAction, double[] Features);
    private sealed class Stream(int seed)
    {
        internal long Count;
        private readonly Random random = new(seed);
        internal readonly List<Position> Opening = [];
        internal readonly List<Position> Reservoir = [];
        internal readonly Queue<Position> Recent = [];
        internal void Add(Position position)
        {
            Count++;
            if (Opening.Count < 2) Opening.Add(position);
            else
            {
                var seen = Count - 2;
                if (Reservoir.Count < 24) Reservoir.Add(position);
                else { var slot = random.NextInt64(seen); if (slot < 24) Reservoir[(int)slot] = position; }
            }
            Recent.Enqueue(position);
            if (Recent.Count > 4) Recent.Dequeue();
        }
        internal Position[] Retained() => Opening.Concat(Reservoir).Concat(Recent)
            .DistinctBy(position => position.Order).OrderBy(position => position.Order).ToArray();
    }
    private readonly object sync = new();
    private readonly int episode;
    private readonly Dictionary<Guid, (Stream Starts, Stream Actions)> players;
    private readonly Dictionary<string, long> actionKinds = new(StringComparer.Ordinal);
    private long order;
    private int sampledStarts, sampledActions;
    private static readonly HashSet<string> SupportedActions = new(StringComparer.Ordinal)
    { "PlayCard", "PerformAttack", "PerformEvolve", "PerformSuperEvolve", "RequestAct", "RequestFusion", "PerformPpBoost", "EndTurn" };

    internal AiTrainingPositions(int episode, int seed, Guid first, Guid second)
    {
        if (episode < 0 || first == Guid.Empty || second == Guid.Empty || first == second) throw new ArgumentException("无效的训练对局身份");
        this.episode = episode;
        players = new();
        foreach (var (id, seat) in new[] { (first, 0), (second, 1) })
        {
            var mix = unchecked(seed + episode * 38531 + seat * 7241);
            players.Add(id, (new(mix), new(unchecked(mix + 31337))));
        }
    }

    internal void Record(Guid actor, bool afterAction, double[] features, string? action = null)
    {
        if (features is not { Length: 512 } || features.Any(value => !double.IsFinite(value))) throw new InvalidDataException("无效的训练局面特征");
        if (afterAction && (action == null || !SupportedActions.Contains(action))) throw new ArgumentException("未知的已结算训练动作");
        lock (sync)
        {
            if (!players.TryGetValue(actor, out var streams)) throw new ArgumentException("训练观察不属于当前对局");
            (afterAction ? streams.Actions : streams.Starts).Add(new(++order, afterAction, (double[])features.Clone()));
            if (afterAction) actionKinds[action!] = actionKinds.GetValueOrDefault(action!) + 1;
        }
    }

    private static IEnumerable<Position> Evenly(Position[] positions, int limit)
    {
        var count = Math.Min(limit, positions.Length);
        for (var i = 0; i < count; i++)
            yield return positions[count == 1 ? 0 : i * (positions.Length - 1) / (count - 1)];
    }

    internal IReadOnlyList<AiTrainingSample> Samples(IReadOnlyDictionary<Guid, string> deckKeys, Guid? winner)
    {
        lock (sync)
        {
            if (winner.HasValue && winner != Guid.Empty && !players.ContainsKey(winner.Value)) throw new ArgumentException("未知的训练胜方");
            var samples = new List<AiTrainingSample>(); sampledStarts = sampledActions = 0;
            foreach (var (actor, streams) in players)
            {
                if (!deckKeys.TryGetValue(actor, out var key) || string.IsNullOrWhiteSpace(key)) throw new ArgumentException("缺少训练牌组身份");
                var starts = streams.Starts.Retained(); var actions = streams.Actions.Retained();
                // Equal quotas prevent a long combo from crowding out turn-start positions; fill unused slots.
                var selected = Evenly(starts, 8).Concat(Evenly(actions, 8)).ToList();
                var used = selected.Select(position => position.Order).ToHashSet();
                var remaining = starts.Concat(actions).Where(position => !used.Contains(position.Order)).OrderBy(position => position.Order).ToArray();
                selected.AddRange(Evenly(remaining, 16 - selected.Count));
                foreach (var position in selected.OrderBy(position => position.Order))
                {
                    if (position.AfterAction) sampledActions++; else sampledStarts++;
                    samples.Add(new(episode, key, (double[])position.Features.Clone(),
                        winner == null || winner == Guid.Empty ? 0 : winner == actor ? 1 : -1) {AfterAction=position.AfterAction});
                }
            }
            return samples;
        }
    }

    internal AiTrainingCoverage Coverage
    {
        get
        {
            lock (sync) return new(players.Values.Sum(value => value.Starts.Count), players.Values.Sum(value => value.Actions.Count),
                players.Values.Sum(value => value.Starts.Retained().Length), players.Values.Sum(value => value.Actions.Retained().Length),
                sampledStarts, sampledActions) { ActionKinds = new(actionKinds, StringComparer.Ordinal) };
        }
    }

    internal IDisposable Attach(GameState game, Func<Guid, GameStateDto> snapshot)
    {
        if (!game.IsLocalAiTraining || players.Keys.Any(id => game.GetPlayerState(id) == null)) throw new InvalidOperationException("仅可观察本地训练对局");
        Action<Guid, string> callback = (actor, kind) => Record(actor, true, AiFeatures.Value(snapshot(actor), actor), kind);
        if (Interlocked.CompareExchange(ref game.LocalAiTrainingObservation, callback, null) != null)
            throw new InvalidOperationException("训练对局已有观察者");
        return new Lease(game, callback);
    }
    private sealed class Lease(GameState game, Action<Guid, string> callback) : IDisposable
    {
        public void Dispose() => Interlocked.CompareExchange(ref game.LocalAiTrainingObservation, null, callback);
    }
}
