using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Services;

// Build native rule facts under a short-lived context, never keep it across an HTTP request.
internal static class AiPlanningFacts
{
    internal sealed record Facts(string ContextSource,AiPpRefundFacts.Catalog Refunds,Dictionary<(Guid Uid,int PlayedId),object[]> Targets)
    {internal AiSelectedHandLoss.Rule[] SelectedHandLoss {get;init;}=[];internal AiHandMaterialRules.Step[] HandMaterials{get;init;}=[];
        internal Dictionary<(Guid Uid,int PlayedId),AiPlannedModes.Stage[]> ModeStages{get;init;}=[];}
    internal static Facts Read(GameState game,GameStateDto snapshot,Guid actor,IReadOnlyList<AiPlayCandidates.Candidate> plays,IServiceScopeFactory? scopes)
    {
        var keys=new Dictionary<(Guid,int),object[]>();
        Facts Current(string source){
            var revision=game.StateRevision;
            foreach(var play in plays)keys.TryAdd((play.Original.InstanceId,play.PlayedCardId),AiPlannedTargets.Keys(game,play));
            var modeStages = new Dictionary<(Guid,int),AiPlannedModes.Stage[]>();
            var modeBytes = 0;
            foreach (var play in plays)
            {
                var key = (play.Original.InstanceId, play.PlayedCardId);
                if (modeStages.ContainsKey(key)) continue;
                var stages = AiPlannedModes.Read(game, play);
                var size = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(stages).Length;
                if (stages.Length == 0 || size > 8192 - modeBytes) continue;
                modeStages.Add(key, stages);
                modeBytes += size;
            }
            var refunds=AiPpRefundFacts.ForGame(game,snapshot,actor);
            var losses=AiSelectedHandLoss.Read(game,plays);
            var materials=AiHandMaterialRules.Read(game,plays);
            return game.StateRevision==revision?new(source,refunds,keys){SelectedHandLoss=losses,HandMaterials=materials,ModeStages=modeStages}:new("state-changed",Unavailable("state-changed"),[]);
        }
        try{
            if(scopes!=null){
                using var scope=scopes.CreateScope();var services=scope.ServiceProvider;
                using var context=new GameContext(services,services.GetRequiredService<IMapper>(),services.GetRequiredService<IGameContextAccessor>(),game,actor);
                return Current("scoped-current-native-rules");
            }
            if(GameServiceProvider.CurrentGameContext is {} ambient&&ReferenceEquals(ambient.CurrentGameState,game))return Current("ambient-current-native-rules");
        }catch(Exception e)when(e is not OperationCanceledException and not OutOfMemoryException){return new("unavailable",Unavailable("native-context-unavailable"),[]);}
        return new("unavailable",Unavailable("native-context-unavailable"),[]);
    }
    private static AiPpRefundFacts.Catalog Unavailable(string reason)=>new(false,"No native planning context; absence is not proof of no targets or refunds.",[],reason);
}
