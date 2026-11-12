using System.Reflection;
using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;

internal static class AiResourceLearningTests
{
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    static readonly Type Outcomes=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiActionOutcomes")!;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static JsonElement Packed(params AiCompletedActionFact[] facts)=>JsonSerializer.SerializeToElement(Outcomes.GetMethod("ForModel",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[facts,false]),Json);
    internal static void Run()
    {
        using(var f=new AiActionOutcomeTests.Fixture()){
            f.Own.BloodPool=2;f.AddCard(2,CardZone.Emblem);f.AddCard(2,CardZone.Emblem);f.AddCard(3,CardZone.Emblem,f.Enemy);
            var newEmblem=f.AddCard(3,CardZone.Extra);var root=f.Begin();
            f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId),new DamageEvent(f.Own.Leader,2,f.Source),new AddEmblemEvent(f.Own,[newEmblem]));
            root.MarkCompleted();f.End(root);var fact=f.Facts.Single();
            Check(fact.Before.BloodPool==2&&fact.After.BloodPool==4,"actual self damage blood counter not captured");
            Check(fact.Before.OwnEmblemIds!.SequenceEqual(new[]{2,2})&&fact.After.OwnEmblemIds!.SequenceEqual(new[]{2,2,3})&&fact.Before.EnemyEmblemIds!.SequenceEqual(new[]{3}),"public emblem copies or side lost");
            f.Own.BloodPool=9;f.Own.Emblem.Clear();Check(fact.After.BloodPool==4&&fact.After.OwnEmblemIds.Length==3,"later state mutated frozen resource endpoint");
            var packed=Packed(fact);var columns=packed.GetProperty("columns").EnumerateArray().Select(x=>x.GetString()).ToList();var sample=packed.GetProperty("samples")[0];
            Check(sample.GetProperty("before")[columns.IndexOf("ownBloodPool")].GetInt32()==2&&sample.GetProperty("after")[columns.IndexOf("ownBloodPool")].GetInt32()==4,"model endpoint columns dropped blood");
            Check(sample.GetProperty("after")[columns.IndexOf("ownEmblemIds")].GetArrayLength()==3,"model endpoint columns dropped emblems");
            var raw=JsonSerializer.Serialize(fact,Json);Check(!raw.Contains("2000000999")&&!raw.Contains(f.Own.PlayerId.ToString())&&!raw.Contains(f.Hidden.InstanceId.ToString()),"hidden card/identity entered resource evidence");
            var ids=(IEnumerable<int>)Outcomes.GetMethod("CardIds",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[fact])!;Check(ids.Contains(3),"public emblem names/rules absent from history lookup");
            var old=JsonSerializer.SerializeToElement(fact.Before,Json).EnumerateObject().Where(p=>p.Name is not ("bloodPool" or "ownEmblemIds" or "enemyEmblemIds" or "ownEmblemsOmitted" or "enemyEmblemsOmitted")).ToDictionary(p=>p.Name,p=>p.Value);
            var legacy=JsonSerializer.Deserialize<AiObservedResources>(JsonSerializer.Serialize(old,Json),Json)!;
            Check(legacy.BloodPool==null&&legacy.OwnEmblemIds==null&&legacy.EnemyEmblemIds==null,"legacy missing context fabricated zero/empty");
            var legacyPacked=Packed(fact with{Before=legacy,After=legacy});Check(legacyPacked.GetProperty("samples")[0].GetProperty("before")[columns.IndexOf("ownBloodPool")].ValueKind==JsonValueKind.Null,"legacy null changed while packing");
        }
        using(var f=new AiActionOutcomeTests.Fixture()){
            for(var i=0;i<40;i++)f.AddCard(2,CardZone.Emblem);var root=f.Begin();f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId));root.MarkCompleted();f.End(root);
            Check(f.Facts.Single().Before.OwnEmblemIds!.Length==32&&f.Facts.Single().Before.OwnEmblemsOmitted==8,"oversize emblem context unbounded or omission hidden");
        }
        using(var f=new AiActionOutcomeTests.Fixture()){
            f.Own.Emblem.Add(f.Hidden);var root=f.Begin();f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId));root.MarkCompleted();f.End(root);
            Check(f.Facts.Single().Before.OwnEmblemIds==null&&f.Facts.Single().After.OwnEmblemIds==null&&!JsonSerializer.Serialize(f.Facts,Json).Contains("2000000999"),"malformed emblem registry leaked a hidden hand card");f.Own.Emblem.Remove(f.Hidden);
        }
        Console.WriteLine("PASS public resource context: native blood/emblem changes, immutable endpoints, duplicate copies, capped registries, legacy unknowns, model packing and hidden-data exclusion; real provider calls=0");
    }
}
