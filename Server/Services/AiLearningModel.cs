using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
namespace ShadowverseBeyond.Server.Services;

public sealed class AiNetwork
{
    public int Format { get; set; }
    public int FeatureVersion { get; set; }
    public string SnapshotId { get; set; } = "";
    public long Updates { get; set; }
    public double[] W1 { get; set; } = [];
    public double[] B1 { get; set; } = [];
    public double[] W2 { get; set; } = [];
    public double B2 { get; set; }
    public int[]? Shape { get; set; }
    public string? Data { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] private float[]? unpacked;
    public void Validate(bool action = false)
    {
        if(Format != 1 || Updates < 0) throw new InvalidDataException("模型版本或训练次数无效");
        if (Data != null) {
            if (FeatureVersion != 3 || Shape == null || !Shape.SequenceEqual(new[]{512,2048,1024,256,1})) throw new InvalidDataException("教师网络结构无效");
            var bytes = Convert.FromBase64String(Data); var count = 0;
            for(var i=1;i<Shape.Length;i++) count += Shape[i]*(Shape[i-1]+1);
            if(bytes.Length != count*4) throw new InvalidDataException("教师网络长度无效");
            unpacked=new float[count];
            for(var i=0;i<count;i++) { unpacked[i]=System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i*4,4)); if(!float.IsFinite(unpacked[i]) || Math.Abs(unpacked[i])>=100) throw new InvalidDataException("教师参数无效"); }
        } else {
            var inputs=action?256:512;var hidden=action?64:128;
            if(FeatureVersion != (action?1:3) && (action || FeatureVersion!=2)) throw new InvalidDataException("特征版本无效");
            if(W1.Length != inputs*hidden || B1.Length != hidden || W2.Length != hidden || !W1.Concat(B1).Concat(W2).Append(B2).All(v=>double.IsFinite(v)&&Math.Abs(v)<100)) throw new InvalidDataException("网络参数无效");
        }
    }
    public double Predict(double[] x, bool action = false)
    {
        if(Data != null) {
            var weights=unpacked!;var offset=0;
            for(var layer=1;layer<Shape!.Length;layer++) {
                var input=Shape[layer-1];var width=Shape[layer];var next=new double[width];var bias=offset+input*width;
                for(var j=0;j<width;j++) { var n=(double)weights[bias+j];for(var i=0;i<input;i++) if(x[i]!=0)n+=weights[offset+j*input+i]*x[i];next[j]=(float)(layer==Shape.Length-1?Math.Tanh(n):Math.Max(0,n)); }
                offset=bias+width;x=next;
            }
            return x[0];
        }
        var result=B2;
        for(var j=0;j<B1.Length;j++) { var n=B1[j];for(var i=0;i<x.Length;i++)if(x[i]!=0)n+=W1[j*x.Length+i]*x[i];result+=Math.Tanh(n)*W2[j]; }
        return action?result:Math.Tanh(result);
    }
}
public sealed class AiWeights
{
    public double Board {get;set;}=1; public double Face {get;set;}=1; public double Survival {get;set;}=1;
    public double Cards {get;set;}=1; public double Resource {get;set;}=1; public double Tempo {get;set;}=1; public double Utility {get;set;}=1;
    public string? ValueModel {get;set;} public string? ValueTeacher {get;set;} public string? ActionPolicy {get;set;}
}
public sealed class AiModelArtifact
{
    public string Format {get;set;}="svnebula-native-ai-1";
    public string Version {get;set;}="";
    public AiWeights Weights {get;set;}=new();
    public Dictionary<string,AiWeights> Specialists {get;set;}=new();
    public Dictionary<string,AiNetwork> Models {get;set;}=new();
}
/// <summary>Portable inference only. No deck presets, training data or hidden enemy cards are loaded.</summary>
public sealed class AiLearningModel
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private AiModelArtifact? current;
    private readonly ILogger<AiLearningModel> logger;
    public string ModelPath {get;}
    public AiModelArtifact? Current => Volatile.Read(ref current);
    public AiLearningModel(IWebHostEnvironment environment, IConfiguration config, ILogger<AiLearningModel> log)
    {
        logger=log;ModelPath=Path.GetFullPath(config["AiModel:Path"] ?? Path.Combine(environment.ContentRootPath,"..","data","ai-model","model.json"));
        if(File.Exists(ModelPath)) { try { Reload(); } catch(Exception e) {logger.LogError(e,"AI模型加载失败，使用基础AI");} }
    }
    public static AiModelArtifact Load(string path) {
        using var file=File.OpenRead(path);var model=JsonSerializer.Deserialize<AiModelArtifact>(file,Json) ?? throw new InvalidDataException("模型为空");
        if(model.Format!="svnebula-native-ai-1" || model.Models.Count>128 || model.Specialists.Count>64)throw new InvalidDataException("模型格式无效");
        var validated = new HashSet<(string,bool)>();
        foreach(var weights in model.Specialists.Values.Prepend(model.Weights)) {
            if(!new[]{weights.Board,weights.Face,weights.Survival,weights.Cards,weights.Resource,weights.Tempo,weights.Utility}.All(v=>double.IsFinite(v)&&v>=0.1&&v<=4))throw new InvalidDataException("策略参数无效");
            foreach(var (id,action) in new[]{(weights.ValueModel,false),(weights.ValueTeacher,false),(weights.ActionPolicy,true)})if(id!=null) {
                if(!model.Models.TryGetValue(id,out var net)) throw new InvalidDataException("模型引用缺失");if(validated.Add((id,action)))net.Validate(action);
            }
        }
        return model;
    }
    public void Reload() {var next=Load(ModelPath);Volatile.Write(ref current,next);logger.LogInformation("Loaded AI model {Version}, {Count} specialist models",next.Version,next.Specialists.Count);}
    public static string DeckKey(IEnumerable<int> ids) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ids.Select(id=>id.ToString()).OrderBy(id=>id,StringComparer.Ordinal))))).ToLowerInvariant();
    public AiWeights Weights(IEnumerable<int>? initialDeck=null) {
        var m=Current;if(m==null)return new();
        return initialDeck!=null&&m.Specialists.TryGetValue(DeckKey(initialDeck),out var w)?w:m.Weights;
    }
    public double Score(GameStateDto state, Guid actor, string kind, CardInstanceDto? source, CardInstanceDto? target=null, AiWeights? weights=null)
    {
        var m=Current;if(m==null)return 0;var w=weights??m.Weights;
        var score=0d;
        if(w.ActionPolicy!=null)score+=6*Math.Tanh(m.Models[w.ActionPolicy].Predict(AiFeatures.Action(state,actor,kind,source,target),true)/2);
        // A cheap afterstate estimate complements action ranking; the real engine still validates/resolves every action.
        if (w.ValueModel != null && source != null && kind is "play" or "evolve" or "attack") {
            var players=state.Players.Select(p=>p with{Hand=new(p.Hand),Field=new(p.Field)}).ToList();
            var me=players.First(p=>p.PlayerId==actor);var enemy=players.First(p=>p.PlayerId!=actor);
            if(kind=="play") {me.Hand.RemoveAll(c=>c.InstanceId==source.InstanceId);me.CurrentPP=Math.Max(0,me.CurrentPP-source.CurrentCost);if(source.Type!=CardType.Spell)me.Field.Add(source);}
            if(kind=="evolve") {me.Field=me.Field.Select(c=>c.InstanceId==source.InstanceId?c with{IsEvolved=true,CurrentAttack=c.CurrentAttack+2,CurrentHealth=c.CurrentHealth+2,MaxHealth=c.MaxHealth+2}:c).ToList();me.CurrentEP=Math.Max(0,me.CurrentEP-1);}
            if(kind=="attack"&&target!=null) {
                if(target.Type==CardType.Leader&&enemy.LeaderCard!=null)enemy.LeaderCard=enemy.LeaderCard with{CurrentHealth=enemy.LeaderCard.CurrentHealth-source.CurrentAttack};
                else {enemy.Field=enemy.Field.Select(c=>c.InstanceId==target.InstanceId?c with{CurrentHealth=c.CurrentHealth-source.CurrentAttack}:c).Where(c=>c.CurrentHealth>0).ToList();me.Field=me.Field.Select(c=>c.InstanceId==source.InstanceId?c with{CurrentHealth=c.CurrentHealth-target.CurrentAttack}:c).Where(c=>c.CurrentHealth>0).ToList();}
            }
            var net=m.Models[w.ValueModel];score+=8*net.Predict(AiFeatures.Value(state with{Players=players},actor,net.FeatureVersion));
        }
        return score;
    }
    public double Value(GameStateDto state, Guid actor, AiWeights? weights=null) {
        var m=Current;if(m==null)return 0;var w=weights??m.Weights;
        // The small distilled value network is the live evaluator; large teacher weights are retained for local training.
        return w.ValueModel!=null?m.Models[w.ValueModel].Predict(AiFeatures.Value(state,actor,m.Models[w.ValueModel].FeatureVersion)):0;
    }
    public async Task SaveCandidateAsync(AiModelArtifact candidate,CancellationToken token) {
        var temp=ModelPath+".tmp";Directory.CreateDirectory(Path.GetDirectoryName(ModelPath)!);
        await using(var file=File.Create(temp))await JsonSerializer.SerializeAsync(file,candidate,Json,token);
        var checkedModel=Load(temp);token.ThrowIfCancellationRequested();File.Copy(ModelPath,ModelPath+".previous",true);File.Move(temp,ModelPath,true);Volatile.Write(ref current,checkedModel);
    }
}

public static class AiFeatures
{
    static double Clamp(double n)=>double.IsFinite(n)?Math.Clamp(n,-2,2):0;
    public static uint Hash(string text) { unchecked {uint h=2166136261;foreach(var c in text){h^=c;h*=16777619;}h^=h>>16;h*=0x7feb352d;h^=h>>15;h*=0x846ca68b;h^=h>>16;return h;} }
    static bool Has(CardInstanceDto c,string key)=>c.Keywords.Any(k=>k.ToString()==key);
    public static double[] Value(GameStateDto s,Guid actor,int version=3) {
        var x=new double[512];var me=s.Players.First(p=>p.PlayerId==actor);var enemy=s.Players.First(p=>p.PlayerId!=actor);
        x[0]=1;x[1]=s.Players.Sum(p=>p.TurnNumber)/20d;x[2]=s.CurrentPlayerId==actor?1:-1;
        foreach(var (p,side) in new[]{(me,0),(enemy,1)}) {
            var followers=p.Field.Where(c=>c.Type==CardType.Follower).ToArray();var o=4+side*28;
            var scalars=new[]{(p.LeaderCard?.CurrentHealth??20)/20d,(p.LeaderCard?.MaxHealth??20)/20d,p.CurrentPP/10d,p.MaxPP/10d,p.CurrentEP/2d,p.CurrentSEP/2d,p.Hand.Count/9d,p.DeckCount/40d,p.TurnNumber/15d,p.Field.Count/5d,p.Emblem.Count/5d,p.PlayedCardCountThisTurn/10d,p.Counters.GetValueOrDefault("rally")/20d,p.BloodPool/10d,p.GraveyardCount/30d,(2-p.PpBoostUsesAvailable)/2d,followers.Sum(c=>c.CurrentAttack)/20d,followers.Sum(c=>c.CurrentHealth)/20d};
            for(var i=0;i<scalars.Length;i++)x[o+i]=scalars[i];
            var keys=new[]{"Ward","Storm","Rush","Bane","Ambush","Barrier","Drain"};for(var i=0;i<keys.Length;i++)x[o+18+i]=followers.Count(c=>Has(c,keys[i]))/5d;
            x[o+25]=followers.Where(c=>c.AttacksThisTurn==0).Sum(c=>c.CurrentAttack)/20d;x[o+26]=followers.Count(c=>c.IsEvolved)/5d;x[o+27]=followers.Count(c=>c.IsSuperEvolved)/5d;
        }
        x[60]=((me.LeaderCard?.CurrentHealth??20)-(enemy.LeaderCard?.CurrentHealth??20))/20d;x[61]=(me.Hand.Count-enemy.Hand.Count)/9d;x[62]=(me.CurrentEP-enemy.CurrentEP)/2d;x[63]=(me.CurrentSEP-enemy.CurrentSEP)/2d;
        void HashFeature(string key,double value){var h=Hash(key);var offset=version==3?288:64;var count=version==3?224:448;x[offset+h%count]+=((h>>16&1)==1?1:-1)*value;}
        if(version==3) {
            for(var i=0;i<Math.Min(9,me.Hand.Count);i++) {var c=me.Hand[i];var cost=c.CurrentCost;var v=new[]{1d,cost/10d,(me.CurrentPP-cost)/10d,cost<=me.CurrentPP?1d:0,c.Type==CardType.Follower?1d:0,c.Type==CardType.Amulet?1d:0,c.CurrentAttack/10d,c.CurrentHealth/10d,0d,0d,0d,Has(c,"Storm")?1d:0};Array.Copy(v,0,x,64+i*12,12);HashFeature($"hand:{i}:{c.CardId}",.25);HashFeature($"adjacent:{i}:{c.CardId}:{(i+1<me.Hand.Count?me.Hand[i+1].CardId.ToString():"end")}",.15);}
            foreach(var (p,side) in new[]{(me,0),(enemy,1)})for(var i=0;i<Math.Min(5,p.Field.Count);i++){var c=p.Field[i];var v=new[]{1d,c.CurrentAttack/10d,c.CurrentHealth/10d,c.MaxHealth/10d,c.AttacksThisTurn/2d,c.IsEvolved?1d:0,c.IsSuperEvolved?1d:0,(c.CurrentCountdown??0)/10d,Has(c,"Ward")?1d:0,Has(c,"Barrier")?1d:0};Array.Copy(v,0,x,172+side*50+i*10,10);}
            x[272]=me.IsPpBoostArmed?1:0;x[274]=(5-me.Field.Count)/5d;x[275]=me.Hand.Count(c=>c.CurrentCost<=me.CurrentPP)/9d;x[276]=me.CanEvolve?0:1;
        }
        foreach(var (p,side) in new[]{(me,0),(enemy,1)}) {
            foreach(var (zone,cards) in new[]{("field",p.Field),("crest",p.Emblem),("hand",side==0?me.Hand:new List<CardInstanceDto>())})foreach(var c in cards) {
                HashFeature($"{side}:{zone}:{c.CardId}",version==3?.2:.25);
                foreach(var k in c.Keywords)HashFeature(version==3?$"{side}:{zone}:keyword:{k}":$"{side}:{zone}:{c.CardId}:keyword:{k}",.1);
                foreach(var counter in c.Counters)HashFeature(version==3?$"{side}:{zone}:counter:{counter.Key}":$"{side}:{zone}:{c.CardId}:counter:{counter.Key}",counter.Value/20d);
                HashFeature(version==3?$"{side}:{zone}:fused":$"{side}:{zone}:{c.CardId}:fused",c.FusedCards.Count/8d);
                if(version==3)HashFeature($"{side}:{zone}:activations",c.ActsThisTurn/4d);
                else {HashFeature($"{side}:{zone}:{c.CardId}:ready",c.AttacksThisTurn==0?.15:-.15);HashFeature($"{side}:{zone}:cost:{Math.Min(10,c.Cost)}",.1);HashFeature($"{side}:{zone}:{c.CardId}:power",(c.CurrentAttack+c.CurrentHealth)/40d);}
            }
        }
        foreach(var counter in me.Counters)HashFeature(version==3?$"owner:{counter.Key}":$"counter:{counter.Key}",counter.Value/20d);
        return x.Select(Clamp).ToArray();
    }
    public static double[] Action(GameStateDto s,Guid actor,string kind,CardInstanceDto? source,CardInstanceDto? target=null) {
        var x=new double[256];var k=Array.IndexOf(new[]{"play","attack","act","fusion","evolve","bonusPP","choose","endTurn"},kind);if(k>=0)x[k]=1;
        Array.Copy(Value(s,actor),0,x,8,64);var me=s.Players.First(p=>p.PlayerId==actor);var enemy=s.Players.First(p=>p.PlayerId!=actor);var cost=source?.CurrentCost??0;
        var local=new[]{me.CurrentPP/10d,me.CurrentEP/2d,me.CurrentSEP/2d,me.Field.Count/5d,enemy.Field.Count/5d,me.Hand.Count/9d,(me.LeaderCard?.CurrentHealth??20)/20d,(enemy.LeaderCard?.CurrentHealth??20)/20d,cost/10d,(source?.CurrentAttack??0)/10d,source?.IsEvolved==true?1d:0,source!=null&&me.Hand.FirstOrDefault()?.InstanceId==source.InstanceId?1d:0};
        if(k>=0)Array.Copy(local,0,x,72+k*12,12);
        var detail=new[]{cost/10d,(source?.CurrentAttack??0)/10d,(source?.CurrentHealth??0)/10d,source?.IsEvolved==true?1d:0,source?.IsSuperEvolved==true?1d:0,(double)(source?.AttacksThisTurn??0),(target?.CurrentAttack??0)/10d,(target?.CurrentHealth??0)/10d,target?.OwnerPlayerId==actor?1d:0,target?.InstanceId==enemy.LeaderCard?.InstanceId?1d:0,0d,5-me.Field.Count};Array.Copy(detail,0,x,168,12);
        void Add(string key,double v){var h=Hash(key);x[192+h%64]+=((h>>16&1)==1?1:-1)*v;}
        foreach(var c in new[]{source,target}.Where(c=>c!=null).Cast<CardInstanceDto>()) {Add($"{kind}:{(c.OwnerPlayerId==actor?"self":"enemy")}:{c.CardId}",.3);foreach(var key in c.Keywords)Add($"{kind}:keyword:{key}",.2);foreach(var pair in c.Counters)Add($"{kind}:variable:{pair.Key}",pair.Value/10d);}
        if(kind=="choose"&&target!=null)Add($"choice:{target.CardId}",.3);
        return x.Select(Clamp).ToArray();
    }
}
