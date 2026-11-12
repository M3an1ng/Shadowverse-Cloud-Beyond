using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Services;

public sealed record AiEternalMatchFacts(bool Candidate,bool PolicyEnabled,bool CoreBeforeMulligan,bool SearchBeforeMulligan,
    bool CoreAfterMulligan,bool SearchAfterMulligan,int? FirstEmblemObservedOwnTurn,int CompletedOwnTurns,
    int EmptyAfterOwnTurnWithoutEmblem,bool EmblemPresentAtEnd);

// Read-only benchmark observations, deliberately independent of the feature-vector training sampler.
// FirstEmblemObservedOwnTurn is an observation after the AI returns, not an exact grant-event timestamp.
internal sealed class AiEternalEvaluation
{
    private sealed class Seat(PlayerState player,bool candidate,bool policy)
    {
        internal readonly PlayerState Player=player;
        internal readonly bool Candidate=candidate,Policy=policy;
        internal readonly bool BeforeCore=player.Hand.Any(c=>c.CurrentDefinitionId==AiEternalEngine.Reiss);
        internal readonly bool BeforeSearch=player.Hand.Any(c=>c.CurrentDefinitionId==AiEternalEngine.Search);
        internal bool AfterCore,AfterSearch;
        internal int? FirstEmblem;
        internal int CompletedTurns,EmptyWithoutEmblem;
    }
    private readonly Dictionary<Guid,Seat> seats=[];
    internal AiEternalEvaluation(GameState game,Guid candidate)
    {
        if(!AiEternalEngine.RulesValid())return;
        foreach(var player in new[]{game.Player1,game.Player2}.Where(p=>p.InitialDeckDefinitionIds.Contains(AiEternalEngine.Reiss)
            &&p.InitialDeckDefinitionIds.Contains(AiEternalEngine.Search)))
            seats.Add(player.PlayerId,new(player,player.PlayerId==candidate,game.LegacyEternalEnginePlayerId!=player.PlayerId));
    }
    internal void AfterMulligan(Guid actor)
    {
        if(!seats.TryGetValue(actor,out var seat))return;
        seat.AfterCore=seat.Player.Hand.Any(c=>c.CurrentDefinitionId==AiEternalEngine.Reiss);
        seat.AfterSearch=seat.Player.Hand.Any(c=>c.CurrentDefinitionId==AiEternalEngine.Search);
    }
    internal void AfterOwnTurn(GameState game,Guid actor,int turn)
    {
        if(!seats.TryGetValue(actor,out var seat))return;
        var has=seat.Player.Emblem.Any(c=>c.CurrentDefinitionId==AiEternalEngine.Emblem);
        if(has)seat.FirstEmblem??=turn;
        // A return because the match ended midway does not count as a completed end-turn observation.
        if(game.IsGameOver||game.CurrentPlayerId==actor)return;
        seat.CompletedTurns++;
        if(!has&&seat.Player.Hand.Count==0)seat.EmptyWithoutEmblem++;
    }
    internal AiEternalMatchFacts[] Snapshot()=>seats.Values.Select(s=>new AiEternalMatchFacts(s.Candidate,s.Policy,
        s.BeforeCore,s.BeforeSearch,s.AfterCore,s.AfterSearch,s.FirstEmblem,s.CompletedTurns,s.EmptyWithoutEmblem,
        s.Player.Emblem.Any(c=>c.CurrentDefinitionId==AiEternalEngine.Emblem))).ToArray();
}
