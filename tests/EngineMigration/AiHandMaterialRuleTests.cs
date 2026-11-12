using System.Reflection;
using System.Text.Json;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;

internal static class AiHandMaterialRuleTests
{
    static readonly BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    static readonly Assembly A=typeof(AiService).Assembly;
    static readonly Type T=A.GetType("ShadowverseBeyond.Server.Services.AiHandMaterialRules")!;
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    internal static void Run()
    {
        var own=new PlayerStateDto{PlayerId=Guid.NewGuid(),CurrentPP=3,MaxPP=3};
        CardInstanceDto Card(int id,int cost,CardType type)=>new(){InstanceId=Guid.NewGuid(),OwnerPlayerId=own.PlayerId,CardId=id,Cost=cost,CurrentCost=cost,Type=type,Zone=CardZone.Hand};
        own.Hand=[Card(10,2,CardType.Follower),Card(11,4,CardType.Follower),Card(12,1,CardType.Spell),Card(13,1,CardType.Follower)];
        var candidate=A.GetType("ShadowverseBeyond.Server.Services.AiPlayCandidates+Candidate")!;
        var candidates=Array.CreateInstance(candidate,4);
        for(var i=0;i<4;i++)candidates.SetValue(Activator.CreateInstance(candidate,own.Hand[i],own.Hand[i],i,PlayMode.Base,own.Hand[i].CurrentCost,false,null,own.Hand[i].CardId),i);
        var ruleType=T.GetNestedType("Step",BindingFlags.NonPublic)!;
        object Step(int index,bool block,bool closed)=>new{handIndex=index,cardId=own.Hand[index].CardId,mode="Base",cost=own.Hand[index].CurrentCost,discardKey="target1",abilityDefinitionId=1,blocksPlayWithoutMaterial=block,closedVisibleHandAfterPlay=closed,scope="offline current-hand-only"};
        Array Rules(params object[] rows)=>(Array)JsonSerializer.Deserialize(JsonSerializer.Serialize(rows,Json),ruleType.MakeArrayType(),Json)!;
        var rules=Rules(Step(0,false,true),Step(2,true,false));var emptyRules=Rules();
        var losses=Array.CreateInstance(typeof(AiStrategyPlanner.HandLossRule),0);
        var selected=Array.CreateInstance(A.GetType("ShadowverseBeyond.Server.Services.AiSelectedHandLoss+Rule")!,0);
        var refunds=Array.CreateInstance(A.GetType("ShadowverseBeyond.Server.Services.AiPpRefundFacts+Refund")!,0);
        AiStrategyPlanner.PlayAdvice Play(int i)=>new(i,own.Hand[i].CardId,"Base",own.Hand[i].CurrentCost,"use");
        AiStrategyPlanner.TurnPlan Plan(params AiStrategyPlanner.PlayAdvice[] line)=>new("setup","offline material plan",[],[],Play:line[0],Plays:line);
        JsonElement Validate(AiStrategyPlanner.TurnPlan plan,Array? facts=null)=>JsonSerializer.SerializeToElement(T.GetMethod("Validate",Hidden)!.Invoke(null,[plan,candidates,losses,own,selected,refunds,facts??rules]),Json);
        var four=Validate(Plan(Play(0),Play(2)));Check(four.GetProperty("valid").GetBoolean(),"four-card material line rejected");
        Check(four.GetProperty("handMaterials").GetProperty("hypotheticalDiscards")[0].GetProperty("discardedHandIndex").GetInt32()==1,"future card used as hypothetical material");
        Check(!four.GetProperty("handMaterials").GetProperty("complete").GetBoolean(),"unknown mode after Tutor presented as fully simulated");
        Check(four.GetProperty("handMaterials").GetProperty("hypotheticalRemainingHandIndexes").ValueKind==JsonValueKind.Null,"unknown final hand invented");
        var fourth=own.Hand[3];own.Hand.RemoveAt(3);
        var line=Plan(Play(0),Play(2));
        var legacy=JsonSerializer.SerializeToElement(A.GetType("ShadowverseBeyond.Server.Services.AiPlaySequence")!.GetMethod("Validate",Hidden)!.Invoke(null,[line,candidates,losses,own,selected,refunds]),Json);
        Check(legacy.GetProperty("valid").GetBoolean(),"payment-only control failed to reproduce missed material cost");
        var missing=Validate(line);Check(!missing.GetProperty("valid").GetBoolean()&&missing.GetProperty("error").GetString()!.Contains("素材"),"missing second discard material accepted");
        Check(Validate(line,emptyRules).GetProperty("valid").GetBoolean(),"unknown native facts invented a material prohibition");
        Check(!Validate(line,emptyRules).GetProperty("handMaterials").GetProperty("complete").GetBoolean(),"missing facts labeled complete");
        Check(Validate(line,Rules(Step(0,false,false),Step(2,true,false))).GetProperty("valid").GetBoolean(),"unknown earlier draw/reaction treated as zero supply");
        var reordered=(Array)T.GetMethod("Reorderings",Hidden)!.Invoke(null,[line,candidates,losses,own,selected,refunds,rules])!;
        Check(reordered.Cast<AiStrategyPlanner.PlayAdvice[]>().All(p=>p[0].HandIndex!=0),"infeasible order survived material reorderings");
        Check(reordered.Cast<AiStrategyPlanner.PlayAdvice[]>().Any(p=>p[0].HandIndex==2),"reverse order omitted despite unknown mode needing native revalidation");
        own.Hand.Add(fourth);
        // The checker must not remove real cards or publish a hypothetical assignment as a committed loss.
        Check(own.Hand.Count==4&&own.Hand[1].CardId==11&&four.GetProperty("remainingHandIndexes").GetArrayLength()==2,"hypothetical material mutated actual hand or legacy known-loss result");
        var single=own with{Hand=[own.Hand[0]]};
        var optional=JsonSerializer.SerializeToElement(T.GetMethod("Validate",Hidden)!.Invoke(null,[Plan(Play(0)),candidates,losses,single,selected,refunds,rules]),Json);
        Check(optional.GetProperty("valid").GetBoolean(),"optional last-card discard wrongly blocks card use");
        var strict=Rules(Step(0,true,true));
        var required=JsonSerializer.SerializeToElement(T.GetMethod("Validate",Hidden)!.Invoke(null,[Plan(Play(0)),candidates,losses,single,selected,refunds,strict]),Json);
        Check(!required.GetProperty("valid").GetBoolean(),"required empty material pool accepted");
        var old=JsonSerializer.Deserialize<AiStrategyPlanner.PlayAdviceFact>("{\"turn\":1,\"advice\":{\"handIndex\":0,\"cardId\":10,\"mode\":\"Base\",\"cost\":2,\"intent\":\"use\"}}",Json);
        Check(old!.HandMaterials==null,"legacy missing material status invented a completed check");
        Console.WriteLine("PASS known hand material constraints: original miss, required/optional pool, future-instance protection, unknown prefixes, reorderings and immutable hypothetical witnesses");
    }
}
