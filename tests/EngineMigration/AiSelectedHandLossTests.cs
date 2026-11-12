using System.Reflection;
using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;

internal static class AiSelectedHandLossTests
{
    static readonly BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    static readonly Assembly Assembly=typeof(AiService).Assembly;
    static readonly Type Facts=Assembly.GetType("ShadowverseBeyond.Server.Services.AiSelectedHandLoss")!;
    static readonly Type Sequence=Assembly.GetType("ShadowverseBeyond.Server.Services.AiPlaySequence")!;
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    internal static void Run()
    {
        AbilityConfig Shape()=>new(){Id=12,Type=AbilityType.Triggered,
            Trigger=new(){EventTypes=[EventType.PlayCard],RelationType=TriggerRelationType.TargetSelf,Timing=TriggerTiming.On},
            Targets=new(){["target1"]=new(){PlayerChoiceOptions=new(){IsRequired=true,MinCount=1,MaxCount=1},Query=new(){SourcePoolExpression="context.owner.hand"}}},
            Effects=[new(){Type=EffectType.Discard,TargetSelector=["target1"]}]};
        bool Reads(AbilityConfig c)=>(bool)Facts.GetMethod("DiscardsSelection",Hidden)!.Invoke(null,[c,"target1"])!;
        Check(Reads(Shape()),"plain single selected discard not recognized");
        var tagged=Shape();tagged.AbilityKeywords.Add(new(){Keyword=AbilityKeyword.Choose});tagged.AbilityKeywords.Add(new(){Keyword=AbilityKeyword.Reanimate});Check(Reads(tagged),"ordinary descriptive keywords wrongly treated as activation gates");
        foreach(var mutate in new Action<AbilityConfig>[] {c=>c.IsEnabled=false,c=>c.Trigger.IsEnabled=false,
            c=>c.Conditions.Add(new(){Expression="true"}),c=>c.PreConditions.Add(new(){Expression="true"}),
            c=>c.Effects[0].IsEnabled=false,c=>c.Effects[0].Type=EffectType.Draw,c=>c.Effects[0].RepeatCount=2,
            c=>c.Effects[0].Parameters["unknown"]=new ShadowverseBeyond.Server.Domain.Abilities.Common.StaticValueConfig{Value=1},
            c=>c.Effects.Insert(0,new(){Type=EffectType.Draw}),c=>c.Targets["target1"].PlayerChoiceOptions!.MaxCount=2,
            c=>c.Trigger.RelationType=TriggerRelationType.TargetOther,
            c=>c.Trigger.ValidZones.Add(CardZone.Field),c=>c.AbilityKeywords.Add(new(){Keyword=AbilityKeyword.Intromezzo}),
            c=>c.Properties["guard"]=new StaticValueConfig{Value=1}}){
            var c=Shape();mutate(c);Check(!Reads(c),"unknown/conditional/ambiguous discard inferred");
        }
        var own=new PlayerStateDto{PlayerId=Guid.NewGuid(),CurrentPP=3,MaxPP=3};
        own.Hand=Enumerable.Range(0,3).Select(i=>new CardInstanceDto{InstanceId=Guid.NewGuid(),OwnerPlayerId=own.PlayerId,
            CardId=i==0?100:101,Zone=CardZone.Hand,Cost=1,CurrentCost=1}).ToList();
        var candidate=Assembly.GetType("ShadowverseBeyond.Server.Services.AiPlayCandidates+Candidate")!;
        var candidates=Array.CreateInstance(candidate,3);
        for(var i=0;i<3;i++)candidates.SetValue(Activator.CreateInstance(candidate,own.Hand[i],own.Hand[i],i,PlayMode.Base,1,false,null,own.Hand[i].CardId),i);
        var ruleType=Facts.GetNestedType("Rule",BindingFlags.NonPublic)!;
        Array Rules(bool unambiguous=true)=> (Array)JsonSerializer.Deserialize(JsonSerializer.Serialize(new[]{new{handIndex=0,cardId=100,playedCardId=100,mode="Base",cost=1,key="target1",abilityDefinitionId=12,unambiguousWithoutAbilityId=unambiguous}},Json),ruleType.MakeArrayType(),Json)!;
        var rules=Rules();var noRules=Array.CreateInstance(ruleType,0);
        AiStrategyPlanner.PlayAdvice Play(int i,AiStrategyPlanner.TargetAdvice? target=null)=>new(i,own.Hand[i].CardId,"Base",1,"use",target);
        var target=new AiStrategyPlanner.TargetAdvice("target1","own","hand",1,101,12);
        var source=Play(0,target);var used=Play(1);var duplicate=Play(2);
        AiStrategyPlanner.TurnPlan Plan(params AiStrategyPlanner.PlayAdvice[] line)=>new("setup","offline selected-discard plan",[],[],Play:line[0],Plays:line);
        JsonElement Validate(AiStrategyPlanner.TurnPlan plan,Array? facts=null)=>JsonSerializer.SerializeToElement(Sequence.GetMethod("Validate",Hidden)!.Invoke(null,[plan,candidates,Array.Empty<AiStrategyPlanner.HandLossRule>(),own,facts??rules,null]),Json);
        bool Valid(AiStrategyPlanner.TurnPlan p,Array? facts=null)=>Validate(p,facts).GetProperty("valid").GetBoolean();
        Check(Valid(Plan(source,used),noRules),"legacy arithmetic control did not reproduce missed discard");
        Check(!Valid(Plan(source,used)),"discarded material was allowed as later play");
        var good=Validate(Plan(source,duplicate));Check(good.GetProperty("valid").GetBoolean()&&good.GetProperty("remainingHandIndexes").GetArrayLength()==0,"same-name other instance incorrectly removed");
        Check(!Valid(Plan(used,source)),"already played card accepted as later discard material");
        var reorder=(Array)Sequence.GetMethod("Reorderings",Hidden)!.Invoke(null,[Plan(source,used),candidates,Array.Empty<AiStrategyPlanner.HandLossRule>(),own,rules,null])!;
        Check(reorder.Length==0,"reordering invented a way to both play and discard the same instance");
        Check(Valid(Plan(source with{Target=target with{Key="other"}},used)),"unrelated key constrained line");
        Check(Valid(Plan(source with{Target=target with{Side="enemy"}},used)),"hidden enemy target became own discard");
        Check(Valid(Plan(source with{Target=target with{CardId=999}},used)),"invalid target advice poisoned local fallback");
        Check(Valid(Plan(source with{Target=target with{AbilityDefinitionId=null}},used),Rules(false)),"ambiguous rule was treated as certain discard");
        Check(!Valid(Plan(source,used),Rules(false)),"explicit ability id failed to disambiguate discard");
        Check(!Valid(Plan(source with{Target=target with{AbilityDefinitionId=null}},used)),"unambiguous implicit ability failed");
        Console.WriteLine("PASS selected-discard intent: legacy contradiction reproduced, duplicate instances, consumed material, reordering, ambiguous/conditional rules and fallback boundaries; no provider calls");
    }
}
