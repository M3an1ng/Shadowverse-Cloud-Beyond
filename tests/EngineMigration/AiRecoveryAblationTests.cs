using System.Reflection;
using Microsoft.Extensions.Configuration;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Services;

internal static class AiRecoveryAblationTests
{
    private static readonly Type Policy=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiRecoveryPolicy")!;
    private static readonly BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    private static bool Enabled=>(bool)Policy.GetProperty("Enabled",Hidden)!.GetValue(null)!;
    private static IDisposable Use(GameState game,Guid actor)=>(IDisposable)Policy.GetMethod("Use",Hidden)!.Invoke(null,[game,actor])!;
    internal static void Control(GameState game,Guid actor)
    {
        var state=Policy.GetMethod("For",Hidden)!.Invoke(null,[game])!;
        state.GetType().GetField("DisabledPlayer",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(state,actor);
    }
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    internal static void Boundaries()
    {
        var actor=Guid.NewGuid();var other=Guid.NewGuid();var game=new GameState("recovery-control",new(actor),new(other),actor);
        var separate=new GameState("recovery-separate",new(actor),new(other),actor);
        Check(Enabled,"recovery is not the default policy");Control(game,other);
        var plan=new AiStrategyPlanner.TurnPlan("pressure","test",[],[],Play:new(0,1,"Base",2,"use"));
        object? Emergency()=>typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiPlayCandidates")!
            .GetMethod("ForEmergency",Hidden)!.Invoke(null,[plan,true,false]);
        var effects=new EffectConfig[]{new(){Type=EffectType.Discard},new(){Type=EffectType.Choose}};
        int Effects()=>((System.Collections.ICollection)typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTargetScoring")!
            .GetMethod("FindEffects",Hidden)!.Invoke(null,[effects,"target1"])!).Count;
        using(Use(game,actor)){
            Check(Enabled&&Emergency()!=null&&Effects()==1,"candidate did not retain recovery changes");
            using(Use(game,other))Check(!Enabled&&Emergency()==null&&Effects()==0,"control did not restore pre-recovery emergency/compound-choice behavior");
            Check(Enabled,"nested control scope leaked");
            var answers=Task.WhenAll(Task.Run(()=>{using var scope=Use(game,other);Thread.Sleep(20);return Enabled;}),
                Task.Run(()=>{using var scope=Use(separate,other);Thread.Sleep(10);return Enabled;})).GetAwaiter().GetResult();
            Check(!answers[0]&&answers[1]&&Enabled,"concurrent actor/game policy scopes leaked");
        }
        Check(Enabled,"policy scope survived disposal");
        var request=new LocalTrainingRequest(Guid.NewGuid(),Guid.NewGuid(),12){CompareRecoveryPolicy=true};
        Check((bool)typeof(LocalTrainingRequest).GetProperty("IsBenchmark",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(request)!,"recovery comparison would enter training/fitting");
        Check(!request.CompareStrategy&&!request.CompareLegacy&&!request.Continuous,"comparison contaminated another strategy path");
        Console.WriteLine("PASS recovery ablation: same default, per-game actor isolation, async scopes, emergency/compound-choice control and no-fit request; provider calls=0");
    }
}
