using System.Runtime.CompilerServices;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Services;

public sealed record AiLeaderRecoveryMatchFacts(int Checks, int NativeCandidates, int ReactionBlocks,
    int VerifiedRoutes, int CompletedSteps, int HpGained, AiLeaderRecoveryStep[] Examples, AiLeaderRecoveryBlock[] BlockingExamples);

public sealed record AiLeaderRecoveryBlock(int RecoveryCardId, int BlockingCardId, string Zone, bool Friendly,
    string AbilityType, string Relation, string[] Events, string[] Effects);

public sealed record AiLeaderRecoveryStep(int Turn, int CardId, string Action, int VisibleDamage,
    int ProjectedRecovery, int HealthBefore, int HealthAfter);

internal sealed class AiLeaderRecoveryEvaluation(Guid candidate)
{
    private static readonly ConditionalWeakTable<GameState, AiLeaderRecoveryEvaluation> Evaluations = new();
    private readonly List<AiLeaderRecoveryStep> examples = [];
    private readonly List<AiLeaderRecoveryBlock> blocks = [];
    private int checks, nativeCandidates, reactionBlocks, verifiedRoutes, completedSteps, hpGained;

    internal Guid Candidate { get; } = candidate;

    internal static void Bind(GameState game, Guid actor) => Evaluations.Add(game, new(actor));
    internal static AiLeaderRecoveryEvaluation? For(GameState game) =>
        Evaluations.TryGetValue(game, out var evaluation) ? evaluation : null;

    internal void Check() => checks++;
    internal void NativeCandidate() => nativeCandidates++;
    internal void ReactionBlock() => reactionBlocks++;

    internal void ObserveBlock(CardInstance recovery, CardInstance blocker, AbilityConfig config)
    {
        if (blocks.Count >= 20) return;
        blocks.Add(new(recovery.CurrentDefinitionId, blocker.CurrentDefinitionId, blocker.CurrentZone.ToString(),
            recovery.OwnerPlayerId == blocker.OwnerPlayerId, config.Type.ToString(), config.Trigger.RelationType.ToString(),
            config.Trigger.EventTypes.Select(type => type.ToString()).ToArray(),
            config.Effects.Where(effect => effect.IsEnabled).Select(effect => effect.Type.ToString()).ToArray()));
    }
    internal void Verified() => verifiedRoutes++;

    internal void Commit(int turn, AiTacticalSearch.Plan plan, int incoming, int before, int after)
    {
        completedSteps++;
        hpGained += Math.Max(0, after - before);
        if (examples.Count < 12)
            examples.Add(new(turn, plan.First.Source.CardId, plan.First.Type, incoming, plan.LeaderRecovery, before, after));
    }

    internal AiLeaderRecoveryMatchFacts Snapshot() =>
        new(checks, nativeCandidates, reactionBlocks, verifiedRoutes, completedSteps, hpGained, [.. examples], [.. blocks]);
}
