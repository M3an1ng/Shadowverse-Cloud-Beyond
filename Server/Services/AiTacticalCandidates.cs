using System.Text.Json;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;

namespace ShadowverseBeyond.Server.Services;

// Ephemeral exact-instance choices. Only public references/projections are serialized.
internal static class AiTacticalCandidates
{
    internal sealed record Option(int Index,string Kind,AiLocalTacticalFacts.Location? Source,
        AiLocalTacticalFacts.Location? Target,AiTacticalSearch.Outcome? BasicProjection,bool LocalFirst,
        [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] AiTacticalSearch.Outcome? KnownCombatProjection=null,
        [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? EvolutionSettlement=null);
    internal sealed record Frame(bool Available,Option[] Options,int Omitted,string? Reason,
        string Scope="Current native-checked first actions. Indices refer to this input only. BasicProjection omits triggered abilities. KnownCombatProjection includes only compiled finite precombat effects, not arbitrary reactions. Neither is committed state or a full simulation.")
    {
        [JsonIgnore] internal Dictionary<int,AiTacticalSearch.Move> Bindings {get;init;}=[];
        [JsonIgnore] internal Guid Actor {get;init;}
        [JsonIgnore] internal string Position {get;init;}="";
        [JsonIgnore] internal long Revision {get;init;}
        [JsonIgnore] internal Guid CatalogRevision {get;init;}
    }
    internal sealed record Bound(Option Choice,AiTacticalSearch.Move Action,Guid Actor,string Position,long Revision,Guid CatalogRevision);
    private const int ByteLimit=6144;
    internal static Frame Missing(string reason)=>new(false,[],0,reason);

    internal static Frame Read(GameContext context,GameStateDto snapshot,Guid actor,AiTacticalSearch.Plan? localPlan,long revision)
        =>ReadWithCombat(context,snapshot,actor,localPlan,revision,null);

    internal static Frame ReadWithCombat(GameContext context,GameStateDto snapshot,Guid actor,AiTacticalSearch.Plan? localPlan,long revision,AiCombatProjection.Frame? combatRules)
    {
        combatRules=AiCombatProjection.ForPosition(combatRules,snapshot,actor);
        var game=context.CurrentGameState;var catalogRevision=GameDataProvider.Instance.DataRevision;
        if(game.StateRevision!=revision||game.GameId!=snapshot.GameId||game.CurrentPlayerId!=actor||game.IsGameOver
            ||game.Phase!=GamePhase.Main)return Missing("not-current-native-main-position");
        var own=snapshot.Players.Single(player=>player.PlayerId==actor);
        var enemy=snapshot.Players.Single(player=>player.PlayerId!=actor);
        var refs=new Dictionary<Guid,AiLocalTacticalFacts.Location>();
        bool Add(IEnumerable<CardInstanceDto> cards,string side,string zone) {
            var index=0;foreach(var card in cards) {
                if(card.InstanceId==Guid.Empty||!refs.TryAdd(card.InstanceId,new(side,zone,index++,card.CardId)))return false;
                var actual=game.FindCardInstance(card.InstanceId);
                if(actual==null||actual.CurrentDefinitionId!=card.CardId||actual.OwnerPlayerId!=card.OwnerPlayerId||actual.CurrentZone!=card.Zone)return false;
            }return true;
        }
        if(!Add(own.Field,"own","field")||!Add(enemy.Field,"enemy","field")
            ||own.LeaderCard==null||enemy.LeaderCard==null||!Add([own.LeaderCard],"own","leader")||!Add([enemy.LeaderCard],"enemy","leader"))
            return Missing("ambiguous-or-changed-public-bindings");
        var native=game.GetPlayerState(actor)!;var moves=new List<AiTacticalSearch.Move>();
        foreach(var unit in own.Field) {
            var actual=game.FindCardInstance(unit.InstanceId)!;
            if(unit.CanAttack)foreach(var target in enemy.Field.Concat([enemy.LeaderCard])) {
                var actualTarget=game.FindCardInstance(target.InstanceId)!;
                if(actual.CanPerformAttack(game,native,actualTarget))
                    moves.Add(new("PerformAttack",new AttackActionDto{AttackerUid=unit.InstanceId,TargetUid=target.InstanceId},unit,target));
            }
            if(!actual.CanEvolve()||actual.HasKeyword(CardKeyword.BanEvolve))continue;
            if(native.CanEvolve&&native.IsEvolutionUnlocked&&(native.CurrentEvolutionPoints>=GameConstants.Evolution.EvolutionPointCost||actual.HasKeyword(CardKeyword.FreeEvolve)))
                moves.Add(new("PerformEvolve",new EvolveActionDto{UnitUid=unit.InstanceId.ToString()},unit));
            if(native.CanSuperEvolve&&native.IsSuperEvolutionUnlocked&&native.CurrentSuperEvolutionPoints>=GameConstants.SuperEvolution.SuperEvolutionPointCost)
                moves.Add(new("PerformSuperEvolve",new SuperEvolveActionDto{UnitUid=unit.InstanceId.ToString()},unit));
        }
        moves.Add(new("EndTurn",new {},own.LeaderCard));
        // Keep the local first choice and the end-turn option before bounded remaining alternatives.
        var ordered=moves.OrderByDescending(move=>Same(move,localPlan?.First)).ThenByDescending(move=>move.Type=="EndTurn").ToArray();
        var options=new List<Option>();var bindings=new Dictionary<int,AiTacticalSearch.Move>();
        foreach(var move in ordered) {
            if(options.Count>=40)break;
            var converted = move.Type == "PerformEvolve" && combatRules?.OrdinaryEvolutionReplacements.Contains(move.Source.InstanceId) == true;
            var option=new Option(options.Count,move.Type,move.Type=="EndTurn"?null:refs[move.Source.InstanceId],
                move.Target==null?null:refs[move.Target.InstanceId],move.Type=="EndTurn"?null:AiTacticalSearch.ProjectBasicMove(snapshot,actor,move),Same(move,localPlan?.First),
                move.Type=="PerformAttack"&&combatRules?.RuleCount>0?AiTacticalSearch.ProjectKnownMove(snapshot,actor,move,combatRules):null,
                converted ? "Ordinary evolution resolves as super evolution (+3/+3); uses ordinary evolution resources." : null);
            options.Add(option);
            if(JsonSerializer.SerializeToUtf8Bytes(new Frame(true,options.ToArray(),ordered.Length-options.Count,null)).Length>ByteLimit){options.RemoveAt(options.Count-1);break;}
            bindings.Add(option.Index,move);
        }
        if(game.StateRevision!=revision||GameDataProvider.Instance.DataRevision!=catalogRevision)return Missing("native-position-or-rules-changed-during-choice-read");
        return new(true,options.ToArray(),ordered.Length-options.Count,null){Bindings=bindings,Actor=actor,
            Position=AiStrategyPlanner.PositionKey(own,enemy),Revision=revision,CatalogRevision=catalogRevision};
    }
    internal static Frame ForPosition(Frame? frame,GameStateDto snapshot,Guid actor,long revision)
    {
        var own=snapshot.Players.Single(player=>player.PlayerId==actor);var enemy=snapshot.Players.Single(player=>player.PlayerId!=actor);
        return frame is {Available:true}&&frame.Actor==actor&&frame.Revision==revision
            &&frame.CatalogRevision==GameDataProvider.Instance.DataRevision&&frame.Position==AiStrategyPlanner.PositionKey(own,enemy)?frame:Missing("missing-or-stale-combat-choices");
    }
    internal static Bound? Bind(Frame frame,int index)=>frame.Available&&frame.Bindings.TryGetValue(index,out var move)
        &&frame.Options.FirstOrDefault(option=>option.Index==index) is { } choice?new(choice,move,frame.Actor,frame.Position,frame.Revision,frame.CatalogRevision):null;

    internal static string? CheckNow(Bound bound,GameContext context,GameStateDto snapshot,Guid actor,long revision)
    {
        if(bound.CatalogRevision!=GameDataProvider.Instance.DataRevision)return "card-rules-changed";
        var current=Read(context,snapshot,actor,new AiTacticalSearch.Plan(bound.Action,0,false,false),revision);
        if(!current.Available||bound.Actor!=actor||bound.Revision!=revision||bound.Position!=current.Position)return "position-or-turn-changed";
        return current.Bindings.Values.Any(move=>Same(move,bound.Action))?null:"native-choice-no-longer-available";
    }
    internal static bool Same(AiTacticalSearch.Move? a,AiTacticalSearch.Move? b)=>a!=null&&b!=null&&a.Type==b.Type
        &&a.Source.InstanceId==b.Source.InstanceId&&a.Target?.InstanceId==b.Target?.InstanceId;
}
