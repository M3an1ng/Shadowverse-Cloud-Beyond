using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

public sealed record AiDrawReflectionCitation(int GameIndex,int ActionIndex,string Observation);

public sealed record AiDrawReflectionObservation(int GameIndex,int ActionIndex,string Observation,int Turn,
    int? OwnDeckBefore,int HandBefore,int HandAfter,string Result,AiObservedDrawBatch[] Batches,
    int? ExhaustedBatchIndex,bool DeckExhausted,bool Truncated);

internal static class AiReflectionDrawEvidence
{
    internal sealed record SourceMatch(int? CardId, string Status);
    internal sealed record PositionMatch(int InsightIndex, int GameIndex, int ActionIndex, string Observation,
        string Status, string RuleCompatibility, int? RecordedDeckCount, int CurrentDeckCount,
        int RecordedHandCount, int CurrentHandCount, bool RecordedSuperEvolutionUnlocked,
        bool CurrentSuperEvolutionUnlocked, SourceMatch[] Sources);

    internal static PositionMatch[]? ForPosition(AiStrategyPlanner.StrategyReflection? reflection,
        AiDefinitionStamp currentRules, GameState game, PlayerStateDto player)
    {
        if (reflection == null || game.GetPlayerState(player.PlayerId) is not { } owner)
            return null;

        var visible = player.Field.Concat(player.Emblem).Where(card => card.OwnerPlayerId == player.PlayerId
            && card.Zone is CardZone.Field or CardZone.Emblem).ToLookup(card => card.CardId);
        var native = owner.Field.Concat(owner.Emblem).ToLookup(card => card.InstanceId);
        SourceMatch MatchSource(int? id)
        {
            if (id is not > 0) return new(null, "unknown");
            var candidates = visible[id.Value].ToArray();
            if (candidates.Length == 0) return new(id, "missing");
            var matches = candidates.Select(card => native[card.InstanceId].FirstOrDefault(source =>
                source.OwnerPlayerId == player.PlayerId && source.CurrentDefinitionId == card.CardId
                && source.CurrentZone == card.Zone)).ToArray();
            if (matches.Any(card => card is { IsSilenced: false })) return new(id, "present");
            return new(id, matches.Any(card => card == null) ? "unknown" : "silenced");
        }

        var rows = new List<PositionMatch>();
        foreach (var (insight, index) in reflection.Insights.Select((insight, index) => (insight, index)))
        foreach (var observation in insight.DrawObservations ?? [])
        {
            if (observation.GameIndex < 0 || observation.GameIndex >= reflection.Evidence.Length) continue;
            var lesson = reflection.Evidence[observation.GameIndex];
            if (lesson.CompletedActions is not { } actions
                || observation.ActionIndex < 0 || observation.ActionIndex >= actions.Length) continue;

            var rules = AiDefinitionRules.Compatibility(lesson.DefinitionRules, currentRules, lesson.DefinitionRulesChanged);
            var unlocked = actions[observation.ActionIndex].Before.SuperEvolutionUnlocked;
            var sources = observation.Batches.Select(batch => batch.SourceCardId).Distinct().Select(MatchSource).ToArray();
            var currentHand = player.Hand.Count;
            var different = rules is not ("same-config-and-engine" or "unknown")
                || (observation.OwnDeckBefore is int deck && deck != player.DeckCount)
                || unlocked != player.IsSuperEvolutionUnlocked
                || sources.Any(source => source.Status is "missing" or "silenced");
            var unknown = rules == "unknown" || observation.OwnDeckBefore == null || observation.Truncated
                || sources.Length == 0 || sources.Any(source => source.Status == "unknown");
            var status = different ? "different" : unknown ? "unknown"
                : observation.HandBefore == currentHand ? "similar"
                : observation.HandBefore == 0 ? "requires-empty-hand" : "different";
            rows.Add(new(index, observation.GameIndex, observation.ActionIndex, observation.Observation,
                status, rules, observation.OwnDeckBefore, player.DeckCount, observation.HandBefore, currentHand,
                unlocked, player.IsSuperEvolutionUnlocked, sources));
        }
        return rows.Count == 0 ? null : rows.ToArray();
    }

    internal static AiDrawReflectionObservation? Resolve(AiDrawReflectionCitation? citation,
        AiStrategyPlanner.Lesson[] lessons,IReadOnlySet<int> namedCards,int[] sources)
    {
        if (citation==null || citation.GameIndex<0 || citation.GameIndex>=lessons.Length
            || !sources.Contains(citation.GameIndex)
            || lessons[citation.GameIndex].CompletedActions is not { } actions
            || !AiActionOutcomes.ModelSampleIndices(actions).Contains(citation.ActionIndex))
            return null;

        var action=actions[citation.ActionIndex];
        if (action.Action.Kind!="EndTurn" || action.EndTurnDraws is not {Batches:{Length:>0 and <=8} batches} draws
            || batches.Any(batch=>batch==null || batch.Requested<0 || batch.Resolved<0))
            return null;

        bool Named(AiObservedDrawBatch batch) => batch.SourceCardId is int id && namedCards.Contains(id);
        bool Completed(AiObservedDrawBatch batch) => batch.Requested>0 && batch.Resolved==batch.Requested;
        var exhausted=draws.DeckExhausted && !draws.Truncated && draws.ExhaustedBatchIndex is int index && index>=0 && index<batches.Length
            && batches[index].Resolved<batches[index].Requested ? batches[index] : null;
        var supported=citation.Observation switch
        {
            "first-batch-completed" => Completed(batches[0]) && Named(batches[0]),
            "recorded-batches-completed" => !draws.DeckExhausted && !draws.Truncated
                && batches.All(Completed) && batches.Any(Named),
            "deck-exhausted" => action.Result=="lost" && draws.DeckExhausted && !draws.Truncated
                && exhausted!=null && Named(exhausted),
            _ => false
        };
        return supported ? new(citation.GameIndex,citation.ActionIndex,citation.Observation,action.Turn,draws.OwnDeckBefore,
            action.Before.OwnHand,action.After.OwnHand,action.Result,batches,exhausted==null?null:draws.ExhaustedBatchIndex,
            draws.DeckExhausted,draws.Truncated) : null;
    }
}
