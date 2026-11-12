using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;

namespace ShadowverseBeyond.Server.Services;

// Per-action committed facts only. Never inspect either deck or the enemy's hidden cards.
internal static class AiPlayDrawEvidence
{
    internal sealed record Facts(Guid Source, Guid Actor, Guid[] Drawn);
    internal sealed class Recorder(Guid actor, PlayCardActionDto expected)
    {
        private bool matched;
        private bool invalid;
        private readonly HashSet<Guid> seenEvents = [];
        private readonly HashSet<Guid> drawn = [];
        internal void Observe(IReadOnlyList<IGameEvent> events)
        {
            foreach (var item in events)
            {
                if (!seenEvents.Add(item.EventId)) continue;
                if (seenEvents.Count > 512) { invalid = true; return; }
                if (item is PlayCardEvent play && play.Source?.Player?.PlayerId == actor)
                {
                    if (play.PlayedCard?.InstanceId != expected.CardUid || play.PlayMode != expected.PlayMode
                        || play.PaidCost != expected.PaidCost) invalid = true;
                    else matched = true;
                }
                if (item is DrawEvent draw && draw.DrawingPlayer?.PlayerId == actor)
                    foreach (var card in draw.DrawnCards)
                    {
                        if (card.OwnerPlayerId != actor || card.InstanceId == Guid.Empty || !drawn.Add(card.InstanceId)
                            || drawn.Count > 10) invalid = true;
                    }
            }
        }
        internal Facts? Snapshot() => matched && !invalid && drawn.Count > 0 ? new(expected.CardUid, actor, drawn.ToArray()) : null;
    }
    internal static Recorder? Begin(AiStrategyPlanner.TurnPlan? plan, Guid actor, string type, object data) =>
        plan?.PlayLine?.Steps.FirstOrDefault() is { } step && type == "PlayCard" && data is PlayCardActionDto action
            && action.CardUid == step.Original.InstanceId && action.PlayMode == step.Mode && action.PaidCost == step.Cost
            ? new(actor, action) : null;

    internal static bool Matches(Facts? facts, Guid source, PlayerStateDto before, PlayerStateDto after,
        IReadOnlyList<CardInstanceDto> retained)
    {
        if (facts == null || facts.Source != source || facts.Actor != before.PlayerId || after.PlayerId != before.PlayerId
            || facts.Drawn.Length is < 1 or > 10 || facts.Drawn.Distinct().Count() != facts.Drawn.Length
            || before.DeckCount - after.DeckCount != facts.Drawn.Length || after.DeckCount < 0
            || after.Hand.Count != retained.Count + facts.Drawn.Length) return false;
        if (!retained.Zip(after.Hand).All(pair => pair.First.InstanceId == pair.Second.InstanceId
            && AiStrategyPlanner.CardKey(pair.First) == AiStrategyPlanner.CardKey(pair.Second))) return false;
        var additions = after.Hand.Skip(retained.Count).ToArray();
        return additions.All(card => card.OwnerPlayerId == before.PlayerId && card.InstanceId != Guid.Empty
                && !before.Hand.Any(old => old.InstanceId == card.InstanceId))
            && additions.Select(card => card.InstanceId).ToHashSet().SetEquals(facts.Drawn);
    }
}
