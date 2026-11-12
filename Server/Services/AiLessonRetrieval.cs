using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

public sealed record AiExperienceMatch(int LessonIndex,int HistoryIndexAtRequest,DateTimeOffset? FinishedAt,
    string RuleCompatibility,string Evidence,int EvidenceIndex,int EvidenceTurn,int[] OwnCardIds,int[] EnemyCardIds,
    int[] FocusCardIds,int ResourceChecksMatched,int ResourceChecksKnown)
{
    public string[] ResourceMismatches{get;init;}=[];
    public string[] ResourceUnknown{get;init;}=[];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int[]? DrawSourceIds { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DrawDeckExhausted { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExhaustedDrawSourceId { get; init; }
}
public sealed record AiExperienceRetrievalFact(int Turn,string Decision,int Available,int Selected,int CompatibleSelected,
    int RelevantSelected,AiExperienceMatch[] Matches,
    string Scope="Observed own actions matched to current visible cards and resources; similarity is not causal or counterfactual evidence.");

// Retrieval only, never an automatic rule or score learned from a match result. No hidden enemy zones are read.
internal static class AiLessonRetrieval
{
    internal sealed record Selection(AiStrategyPlanner.Lesson[] Lessons,AiExperienceRetrievalFact Fact);
    private sealed record Episode(string Kind,int Index,int Turn,int? OwnCard,int? EnemyCard,AiObservedResources? Before,
        int? OwnHealth=null,int? EnemyHealth=null,AiObservedEndTurnDraws? Draws=null);
    private sealed record Match(Episode Episode,int[] Own,int[] Enemy,int[] Focus,int ResourceMatched,int ResourceKnown,double Score,string[] Mismatches,string[] Unknown);
    private sealed record Row(AiStrategyPlanner.Lesson Lesson,int Index,string Compatibility,Match? Match);

    private static int? ExhaustedSource(AiObservedEndTurnDraws? draws) =>
        draws is {DeckExhausted:true,Truncated:false,ExhaustedBatchIndex: int index,Batches:{ } batches}
        && index>=0 && index<batches.Length && batches[index].Resolved<batches[index].Requested
        && batches[index].SourceCardId is >0 ? batches[index].SourceCardId : null;

    internal static Selection Select(AiStrategyPlanner.Lesson[] history,string opponentClass,AiDefinitionStamp current,
        GameStateDto snapshot,Guid actor,Func<AiStrategyPlanner.Lesson[],string,AiStrategyPlanner.Lesson[]> fallback,
        string decision,int[]? focusedCards=null)
    {
        var own=snapshot.Players.Single(player=>player.PlayerId==actor);
        var enemy=snapshot.Players.Single(player=>player.PlayerId!=actor);
        static HashSet<int> Visible(PlayerStateDto player,bool hand)=>player.Field.Concat(player.Emblem)
            .Concat(hand?player.Hand:[]).Where(card=>card.Type!=CardType.Leader).Select(card=>card.CardId).ToHashSet();
        var ownIds=Visible(own,true);var enemyIds=Visible(enemy,false);var focus=(focusedCards??[]).Where(id=>id>0).ToHashSet();
        var drawSources = decision=="turn-plan" && snapshot.CurrentPlayerId==actor
            ? own.Field.Concat(own.Emblem).Where(card=>card.OwnerPlayerId==actor
                && card.Zone is CardZone.Field or CardZone.Emblem && card.CardId>0).Select(card=>card.CardId).ToHashSet()
            : [];
        var rows=history.Select((lesson,index)=>new Row(lesson,index,
            AiDefinitionRules.Compatibility(lesson.DefinitionRules,current,lesson.DefinitionRulesChanged),
            Best(lesson,own,enemy,ownIds,enemyIds,focus,drawSources,opponentClass,snapshot.CurrentPlayerId==actor))).ToArray();
        var selected=new List<Row>();
        foreach(var group in rows.GroupBy(row=>row.Compatibility=="same-config-and-engine").OrderByDescending(group=>group.Key)) {
            // Reserve at least half the request for broad, recent, balanced observations.
            var relevant=group.Where(row=>row.Match!=null).OrderByDescending(row=>row.Match!.Score)
                .ThenByDescending(row=>row.Lesson.FinishedAt).ThenByDescending(row=>row.Index).Take(4);
            var fallbackOrder=fallback(group.Select(row=>row.Lesson).ToArray(),opponentClass)
                .Select(lesson=>group.First(row=>ReferenceEquals(row.Lesson,lesson)));
            foreach(var row in relevant.Concat(fallbackOrder)) {
                if(selected.Count==8)break;
                if(selected.All(existing=>existing.Index!=row.Index))selected.Add(row);
            }
            if(selected.Count==8)break;
        }
        var matches=selected.Select((row,index)=>(Row:row,Index:index)).Where(item=>item.Row.Match!=null)
            .Select(item=>{
                var row=item.Row;var match=row.Match!;
                return new AiExperienceMatch(item.Index,row.Index,row.Lesson.FinishedAt,row.Compatibility,match.Episode.Kind,
                    match.Episode.Index,match.Episode.Turn,match.Own,match.Enemy,match.Focus,match.ResourceMatched,match.ResourceKnown){ResourceMismatches=match.Mismatches,ResourceUnknown=match.Unknown,
                        DrawSourceIds=match.Episode.Draws!=null?match.Own:null,
                        DrawDeckExhausted=match.Episode.Draws?.DeckExhausted,
                        ExhaustedDrawSourceId=ExhaustedSource(match.Episode.Draws)};
            }).ToArray();
        return new(selected.Select(row=>row.Lesson).ToArray(),new(own.TurnNumber,decision,history.Length,selected.Count,
            selected.Count(row=>row.Compatibility=="same-config-and-engine"),matches.Length,matches));
    }

    private static IEnumerable<Episode> Episodes(AiStrategyPlanner.Lesson lesson)
    {
        foreach(var (action,index) in (lesson.CompletedActions??[]).Select((action,index)=>(action,index))) {
            yield return new("completedActions",index,action.Turn,action.ActorBefore?.CardId,action.TargetBefore?.CardId,action.Before);
            if(action.Action.Kind=="EndTurn" && action.EndTurnDraws is { } draws)
                yield return new("completedActions",index,action.Turn,null,null,action.Before,Draws:draws);
            // Match only targets actually selected within a completed root, never proposed targets or final health changes.
            foreach(var t in (action.ChoiceTargets?.Requests??[]).Take(4).SelectMany(r=>r.Targets.Take(4)).Select(t=>t.Before)) {
                if(t.Side=="enemy"&&t.Zone=="field")yield return new("completedActions",index,action.Turn,action.ActorBefore?.CardId,t.CardId,action.Before);
                else if(t.Side=="own"&&t.Zone is "hand" or "field")yield return new("completedActions",index,action.Turn,t.CardId,null,action.Before);
            }
        }
        foreach(var (attack,index) in (lesson.Attacks??[]).Select((attack,index)=>(attack,index)))
            yield return new("attacks",index,attack.Turn,attack.AttackerCardId,attack.TargetCardId,null,attack.OwnHealth,attack.EnemyHealth);
    }
    private static Match? Best(AiStrategyPlanner.Lesson lesson,PlayerStateDto own,PlayerStateDto enemy,
        HashSet<int> ownIds,HashSet<int> enemyIds,HashSet<int> focus,HashSet<int> drawSources,string opponentClass,bool actorTurn)
    {
        Match? best=null;
        foreach(var episode in Episodes(lesson)) {
            var a=episode.OwnCard is int id&&ownIds.Contains(id)?new[]{id}:Array.Empty<int>();
            if(episode.Draws is { } draws && drawSources.Count>0)
                a=(draws.Batches??[]).Take(8).Where(batch=>batch.Requested>0)
                    .Select(batch=>batch.SourceCardId).OfType<int>().Where(drawSources.Contains).Distinct().ToArray();
            var b=episode.EnemyCard is int target&&enemyIds.Contains(target)?new[]{target}:Array.Empty<int>();
            var f=episode.OwnCard is int source&&focus.Contains(source)?new[]{source}:Array.Empty<int>();
            if(a.Length+b.Length+f.Length==0)continue;
            var exhaustedSource=ExhaustedSource(episode.Draws);
            var matched=0;var known=0;var keyMatches=0;var mismatches=new List<string>();var unknown=new List<string>();
            void Test(bool condition,string? field=null){known++;if(condition)matched++;else if(field!=null)mismatches.Add(field);}
            Test(Math.Abs((long)episode.Turn-own.TurnNumber)<=2,"turn-window");
            if(episode.Before is { } before) {
                Test(Math.Abs((long)before.PP-own.CurrentPP)<=2,"pp-window");
                Test(Math.Abs((long)before.MaxPP-own.MaxPP)<=2,"max-pp-window");
                Test(before.BoostArmed==own.IsPpBoostArmed&&before.BoostRemaining==own.PpBoostUsesAvailable,"pp-boost");
                Test(before.ActiveSide==(actorTurn?"own":"enemy"),"active-side");
                Test(Math.Abs((long)before.OwnHand-own.Hand.Count)<=1,"own-hand-window");
                Test(Math.Abs((long)before.OwnField-own.Field.Count)<=1,"own-field-window");
                Test(Math.Abs((long)before.EnemyField-enemy.Field.Count)<=1,"enemy-field-window");
                Test(before.CanEvolve==own.CanEvolve&&before.EP==own.CurrentEP,"evolution-resource");
                Test(before.CanSuperEvolve==own.CanSuperEvolve&&before.SEP==own.CurrentSEP,"super-evolution-resource");
                Test(Math.Abs((long)before.OwnGraveyard-own.GraveyardCount)<=2,"own-graveyard-window");
                if(before.BloodPool is int blood){var same=blood==own.BloodPool;Test(same,"own-blood-pool");if(same)keyMatches++;}else unknown.Add("own-blood-pool");
                void Emblems(int[]? recorded,int omitted,PlayerStateDto current,string field){
                    var present=AiPublicResourceContext.CurrentEmblems(current);
                    if(recorded==null||omitted!=0||present==null){unknown.Add(field);return;}
                    var same=recorded.Order().SequenceEqual(present);Test(same,field);if(same)keyMatches++;
                }
                if(episode.Draws is { } recorded)
                {
                    Test((before.OwnHand==0)==(own.Hand.Count==0),"empty-hand-state");
                    Test(before.SuperEvolutionUnlocked==own.IsSuperEvolutionUnlocked,"super-evolution-unlocked");
                    if(recorded.OwnDeckBefore is int count)
                        Test(count==own.DeckCount,"own-deck-count");
                    else unknown.Add("own-deck-count");
                    if (recorded.DeckExhausted)
                    {
                        if (exhaustedSource is int failedSource)
                            Test(drawSources.Contains(failedSource),"exhausted-draw-source-missing");
                        else unknown.Add("exhausted-draw-source");
                    }
                }
                Emblems(before.OwnEmblemIds,before.OwnEmblemsOmitted,own,"own-emblems");
                Emblems(before.EnemyEmblemIds,before.EnemyEmblemsOmitted,enemy,"enemy-emblems");
            }
            else unknown.AddRange(["completed-resource-snapshot","own-blood-pool","own-emblems","enemy-emblems"]);
            if((episode.Before?.OwnHealth??episode.OwnHealth) is int ownHealth&&own.LeaderCard is { } ownLeader)
                Test(Math.Abs((long)ownHealth-ownLeader.CurrentHealth)<=4,"own-health-window");
            if((episode.Before?.EnemyHealth??episode.EnemyHealth) is int enemyHealth&&enemy.LeaderCard is { } enemyLeader)
                Test(Math.Abs((long)enemyHealth-enemyLeader.CurrentHealth)<=4,"enemy-health-window");
            // Match the failed draw's actual source before reusing the outcome of a preceding draw.
            var failedSourceMatch=exhaustedSource is int failed && drawSources.Contains(failed);
            var failedSourceMissing=exhaustedSource.HasValue && !failedSourceMatch;
            var sameDrawResources=episode.Draws?.OwnDeckBefore==own.DeckCount
                && episode.Before?.SuperEvolutionUnlocked==own.IsSuperEvolutionUnlocked;
            // Game result, model explanation, proposed actions and missing records are not scoring inputs.
            var score=4*Math.Min(a.Length,2)+2*b.Length+5*f.Length+(lesson.OpponentClass==opponentClass?1:0)
                +(double)matched/known+(episode.Before!=null?.25:0)+.05*keyMatches+(failedSourceMissing?-2:failedSourceMatch && sameDrawResources?2:0);
            if(best==null||score>best.Score)best=new(episode,a,b,f,matched,known,score,mismatches.ToArray(),unknown.ToArray());
        }
        return best;
    }
}
