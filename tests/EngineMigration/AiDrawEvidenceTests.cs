using System.Reflection;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Services;

internal static class AiDrawEvidenceTests
{
    private static readonly Type Evidence=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiPlayDrawEvidence")!;
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    public static void Run()
    {
        var actor=Guid.NewGuid();var own=new PlayerState(actor);var enemy=new PlayerState(Guid.NewGuid());
        var source=new CardInstance(Guid.NewGuid(),1,actor);var drawn=new CardInstance(Guid.NewGuid(),1,actor);
        var action=new PlayCardActionDto{CardUid=source.InstanceId,PlayMode=PlayMode.Base,PaidCost=1};
        object Recorder()=>Activator.CreateInstance(Evidence.GetNestedType("Recorder",BindingFlags.NonPublic)!,[actor,action])!;
        void Observe(object recorder,params IGameEvent[] events)=>recorder.GetType().GetMethod("Observe",Hidden)!.Invoke(recorder,[events]);
        object? Snapshot(object recorder)=>recorder.GetType().GetMethod("Snapshot",Hidden)!.Invoke(recorder,null);
        DrawEvent Draw(PlayerState player,params CardInstance[] cards){var ev=new DrawEvent(player,cards.Length);foreach(var card in cards)ev.Target.Add(TargetInfo.ForCard(card));return ev;}
        var play=new PlayCardEvent(own,source,PlayMode.Base,1);var draw=Draw(own,drawn);
        var recorder=Recorder();Observe(recorder,play,draw);var facts=Snapshot(recorder);Check(facts!=null,"committed matching play/draw evidence lost");
        Observe(recorder,play,draw);Check(Snapshot(recorder)!=null,"duplicate delivery of the same event poisoned evidence");
        var unbound=Recorder();Observe(unbound,draw);Check(Snapshot(unbound)==null,"draw without matching committed play was accepted");
        var other=Recorder();Observe(other,new PlayCardEvent(own,new(Guid.NewGuid(),1,actor),PlayMode.Base,1),draw);Check(Snapshot(other)==null,"unrelated committed play authorized resumption");
        var wrongMode=Recorder();Observe(wrongMode,new PlayCardEvent(own,source,PlayMode.Enhance,1),draw);Check(Snapshot(wrongMode)==null,"changed play mode authorized resumption");
        var wrongCost=Recorder();Observe(wrongCost,new PlayCardEvent(own,source,PlayMode.Base,2),draw);Check(Snapshot(wrongCost)==null,"changed paid cost authorized resumption");
        var hidden=Recorder();Observe(hidden,play,Draw(enemy,new CardInstance(Guid.NewGuid(),1,enemy.PlayerId)));Check(Snapshot(hidden)==null,"opponent draw was treated as own known cards");
        var foreign=Recorder();Observe(foreign,play,Draw(own,new CardInstance(Guid.NewGuid(),1,enemy.PlayerId)));Check(Snapshot(foreign)==null,"foreign card entered own draw evidence");
        var duplicateCard=Recorder();Observe(duplicateCard,play,Draw(own,drawn),Draw(own,drawn));Check(Snapshot(duplicateCard)==null,"same card drawn twice across events was collapsed silently");
        var tooMany=Recorder();Observe(tooMany,play,Draw(own,Enumerable.Range(0,11).Select(_=>new CardInstance(Guid.NewGuid(),1,actor)).ToArray()));Check(Snapshot(tooMany)==null,"draw evidence exceeded bounded hand-sized scope");
        var retained=new CardInstanceDto{InstanceId=Guid.NewGuid(),OwnerPlayerId=actor,CardId=2,CurrentCost=2,Type=CardType.Follower};
        var addition=new CardInstanceDto{InstanceId=drawn.InstanceId,OwnerPlayerId=actor,CardId=1,Type=CardType.Spell};
        var before=new PlayerStateDto{PlayerId=actor,DeckCount=2,Hand=[new(){InstanceId=source.InstanceId,OwnerPlayerId=actor},retained]};
        var after=new PlayerStateDto{PlayerId=actor,DeckCount=1,Hand=[retained,addition]};
        bool Matches(PlayerStateDto value,object? proof=null)=>(bool)Evidence.GetMethod("Matches",Hidden)!.Invoke(null,[proof??facts,source.InstanceId,before,value,new[]{retained}])!;
        Check(Matches(after),"actual appended draw with unchanged retained cards was rejected");
        foreach(var changed in new[]{
            after with{DeckCount=2},after with{Hand=[addition,retained]},
            after with{Hand=[retained with{EffectDescriptions=["changed ability"]},addition]},
            after with{Hand=[retained,addition with{OwnerPlayerId=enemy.PlayerId}]},
            after with{Hand=[retained,addition with{InstanceId=source.InstanceId}]},
            after with{Hand=[retained,addition,addition]},after with{PlayerId=enemy.PlayerId}
        })Check(!Matches(changed),"inconsistent retained hand, deck count, instance or ownership was accepted");
        Check(!(bool)Evidence.GetMethod("Matches",Hidden)!.Invoke(null,[null,source.InstanceId,before,after,new[]{retained}])!,"missing evidence was accepted");
        Console.WriteLine("PASS action-bound committed draws; duplicate delivery, foreign/unknown cards, hand mutations and deck counts guarded; real provider calls=0");
    }
}
