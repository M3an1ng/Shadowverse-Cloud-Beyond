using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Services;

public sealed record AiOptionComparisonFact(string Policy, int[] LocalIndexes, int[]? LocalCardIds, bool Different);

// The selected and offered instance IDs only live here, never in a lesson or model payload.
internal sealed class AiOptionComparisons
{
    private sealed record Pending(Guid Request, string Kind, Guid? Source, Guid[] Offered,
        Guid[] Local, Guid[] Selected, int[]? LocalCardIds, string Policy);
    private readonly List<Pending> pending = [];
    private int turn = -1;
    internal int Different { get; private set; }

    internal void Record(int currentTurn, PlayerChoiceRequest request, IReadOnlyList<Guid> local,
        IReadOnlyList<Guid> selected, string policy)
    {
        if (turn != currentTurn)
        {
            pending.Clear();
            turn = currentTurn;
        }
        var offered = request.SelectableTargetIds.ToArray();
        bool Valid(IReadOnlyList<Guid> ids) => ids.Count >= request.MinSelectionCount
            && ids.Count <= request.MaxSelectionCount && ids.Distinct().Count() == ids.Count && ids.All(offered.Contains);
        if (request.PendingChoiceId == Guid.Empty || request.TargetKey is not ("decision" or "discover")
            || offered.Length is < 2 or > 12 || offered.Contains(Guid.Empty)
            || offered.Distinct().Count() != offered.Length || !Valid(local) || !Valid(selected)
            || local.Count != selected.Count || local.Count == 0) return;
        var cardIds = request.TargetKey == "discover"
            ? local.Select(id => request.SelectableCardInstances.FirstOrDefault(card => card.InstanceId == id)?.CardId ?? 0).ToArray()
            : null;
        if (cardIds?.Any(id => id <= 0) == true) return;
        pending.RemoveAll(item => item.Request == request.PendingChoiceId);
        if (pending.Count == 32) pending.RemoveAt(0);
        pending.Add(new(request.PendingChoiceId, request.TargetKey, request.OptionSourceCard?.InstanceId,
            offered, local.ToArray(), selected.ToArray(), cardIds, policy));
    }

    internal AiOptionComparisonFact? CompleteMode(int currentTurn, ChooseEvent choice)
    {
        if (turn != currentTurn || choice.SourceCard == null || choice.SelectedOptionIds.Count == 0
            || choice.SelectedOptionIds.Any(index => index < 0 || index >= choice.OptionIds.Count)) return null;
        var offered = choice.OptionIds.ToArray();
        var selected = choice.SelectedOptionIds.Select(index => offered[index]).ToArray();
        return Complete(pending.Where(item => item.Kind == "decision" && item.Source == choice.SourceCard.InstanceId
            && item.Offered.Length == offered.Length && item.Offered.All(offered.Contains)
            && Same(item.Selected, selected)).ToArray(), offered);
    }

    internal AiOptionComparisonFact? CompleteDiscover(int currentTurn, Guid selected)
    {
        if (turn != currentTurn) return null;
        var candidates = pending.Where(item => item.Kind == "discover"
            && item.Selected.Length == 1 && item.Selected[0] == selected).ToArray();
        return Complete(candidates, candidates.Length == 1 ? candidates[0].Offered : []);
    }

    internal void ClearPending() => pending.Clear();

    private static bool Same(Guid[] first, Guid[] second) => first.Length == second.Length
        && first.Order().SequenceEqual(second.Order());

    private AiOptionComparisonFact? Complete(Pending[] candidates, Guid[] offered)
    {
        // Reusing a discover instance across unresolved requests makes the join ambiguous.
        foreach (var candidate in candidates) pending.Remove(candidate);
        if (candidates.Length != 1) return null;
        var item = candidates[0];
        var different = !Same(item.Local, item.Selected);
        if (different) Different++;
        return new(item.Policy, item.Local.Select(id => Array.IndexOf(offered, id) + 1).ToArray(), item.LocalCardIds, different);
    }
}
