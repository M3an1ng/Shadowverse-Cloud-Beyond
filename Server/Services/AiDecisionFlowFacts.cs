using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

public sealed record AiPriorChoiceStage(int EffectIndex,int ExecutionIndex,string[] RuleFingerprints);
public sealed record AiChoiceFlowStamp(string Phase,int EffectIndex,int ExecutionIndex,string? ParentRulesFingerprint,
    bool PriorChoicesKnown,int PriorChoicesObserved,int PriorChoicesRetained,int PriorChoicesOmitted,AiPriorChoiceStage[] PriorChoices,int Schema=1)
{
    public AiNativeActionFact? NativeAction {get;init;}
}

// Native request order, not an ability simulator. Earlier proposed choices are never committed hand/board facts.
internal static class AiDecisionFlowFacts
{
    private sealed record Binding(Guid Event,Guid Parent,CardInstance Source,int Effect,int Execution,bool Preflight,AbilityConfig Config,GameState? Game,long Revision,int Turn,AiNativeActionFacts.Capture NativeAction);
    private sealed record Rule(string? Fingerprint,JsonElement? Body,int Bytes,string? Omission);
    private sealed record Proposed(int Effect,int Execution,Rule[] Rules);
    private sealed class State
    {
        internal Guid Event,Parent,Source;internal long Revision;internal int Turn;internal bool Preflight,CountingStopped;
        internal string? ParentHash;internal Dictionary<(int Effect,int Execution),Proposed> Choices=[];
    }
    internal sealed record Prior(int EffectIndex,int ExecutionIndex,string?[] RuleFingerprints,JsonElement[]? Rules,string? Omission,int[][] CurrentOptionIndexes);
    internal sealed record Catalog(bool Available,string? Reason,string? Phase,int? SourceCardId,string? SourceSide,int? EffectIndex,int? ExecutionIndex,
        string? ParentRulesFingerprint,JsonElement? ParentRule,Prior[] PriorProposals,AiChoiceFlowStamp? Stamp,int ContentBytes,int ByteLimit,string[] Omitted,
        string Scope="Native effect invocation and earlier proposed choices only. Preflight happens before parent effects settle: actual board/hand/PP must not be treated as an afterstate. Prior proposals are not generated cards, successful effects or completed actions. DSL is preserved, not evaluated; unknown/missing context is not an empty history.")
    {
        [JsonIgnore] internal int[] ReferencedCardIds {get;init;}=[];
        public int OmittedReferenceCount {get;init;}
        public AiNativeActionFact? NativeAction {get;init;}
        public string? NativeActionOmission {get;init;}
    }
    internal sealed record Invocation(Guid Source, int PlayedId, Guid Event, long AbilityDefinitionId,
        int Effect, int Execution, string ParentFingerprint);
    internal static Invocation? Identify(GameState game, PlayerChoiceRequest request)
    {
        if (!Bindings.TryGetValue(request, out var binding) || !Valid(game, request, binding)
            || AiPlannedModes.Fingerprint(binding.Config) is not { } fingerprint) return null;
        return new(binding.Source.InstanceId, binding.Source.CurrentDefinitionId, binding.Event, binding.Config.Id,
            binding.Effect, binding.Execution, fingerprint);
    }
    private static readonly ConditionalWeakTable<PlayerChoiceRequest,Binding> Bindings=new();
    private static readonly ConditionalWeakTable<GameState,State> States=new();
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    internal static void Bind(PlayerChoiceRequest request,Effect effect,Ability parent,bool preflight)
    {
        var game=GameServiceProvider.CurrentGameContext?.CurrentGameState;
        if(parent.SourceCard is {} source)Bindings.Add(request,new(effect.TriggeredEventId,parent.RuntimeId,source,effect.EffectIndex,effect.ExecutionIndex,preflight,parent.Config,
            game,game?.StateRevision??-1,game?.GetPlayerState(request.RequestingPlayerId)?.TurnNumber??-1,
            AiNativeActionFacts.Read(GameServiceProvider.CurrentGameContext,request.RequestingPlayerId,source)));
    }
    private static bool Valid(GameState game,PlayerChoiceRequest request,Binding binding)=>request.TargetKey=="decision"
        &&ReferenceEquals(binding.Game,game)&&binding.Revision==game.StateRevision&&game.GetPlayerState(request.RequestingPlayerId)?.TurnNumber==binding.Turn
        &&binding.Event!=Guid.Empty&&binding.Parent!=Guid.Empty&&binding.Effect is >=0 and <=1024&&binding.Execution is >=0 and <=1024
        &&request.SelectableTargetIds.Count is >=2 and <=12&&request.SelectableTargetIds.All(id=>id!=Guid.Empty)
        &&request.SelectableTargetIds.Distinct().Count()==request.SelectableTargetIds.Count
        &&game.GetPlayerState(request.RequestingPlayerId)!=null&&ReferenceEquals(game.FindCardInstance(binding.Source.InstanceId),binding.Source)
        &&(binding.Source.OwnerPlayerId==request.RequestingPlayerId||binding.Source.CurrentZone is CardZone.Field or CardZone.Emblem);
    private static Rule Pack(AbilityConfig config,int retentionLimit=int.MaxValue)
    {
        try{
            if(!AiRuntimeAbilityFacts.SafeConfiguration(config))return new(null,null,0,"unsafe-configuration");
            var body=AiAbilityFacts.Configuration(config);if(AiRuntimeAbilityFacts.ContainsPrivateData(body))return new(null,null,0,"private-configuration");
            var bytes=JsonSerializer.SerializeToUtf8Bytes(body,Json);var fingerprint=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            return bytes.Length<=retentionLimit?new(fingerprint,body,bytes.Length,null):new(fingerprint,null,0,"rule-retention-budget");
        }catch(Exception error) when(error is JsonException or InvalidOperationException or NotSupportedException or ArgumentException){return new(null,null,0,"unreadable-configuration");}
    }
    private static State Prepare(GameState game,PlayerChoiceRequest request,Binding binding,string? parentHash)
    {
        var state=States.GetOrCreateValue(game);var turn=game.GetPlayerState(request.RequestingPlayerId)!.TurnNumber;
        if(state.Event!=binding.Event||state.Parent!=binding.Parent||state.Source!=binding.Source.InstanceId||state.Revision!=game.StateRevision
            ||state.Turn!=turn||state.Preflight!=binding.Preflight||state.ParentHash!=parentHash){
            state.Event=binding.Event;state.Parent=binding.Parent;state.Source=binding.Source.InstanceId;state.Revision=game.StateRevision;
            state.Turn=turn;state.Preflight=binding.Preflight;state.ParentHash=parentHash;state.CountingStopped=false;state.Choices.Clear();
        }
        return state;
    }
    internal static void Remember(GameState? game,PlayerChoiceRequest request,IReadOnlyList<Guid> selected)
    {
        if(game==null||!Bindings.TryGetValue(request,out var binding)||!Valid(game,request,binding)
            ||selected.Count<request.MinSelectionCount||selected.Count>request.MaxSelectionCount||selected.Distinct().Count()!=selected.Count
            ||selected.Any(id=>!request.SelectableTargetIds.Contains(id)||!request.DecisionOptions.ContainsKey(id)))return;
        var parent=Pack(binding.Config,0);var state=States.GetOrCreateValue(game);
        lock(state){
            Prepare(game,request,binding,parent.Fingerprint);
            var key=(binding.Effect,binding.Execution);
            // Repeating a request replaces this slot, without adding a fictitious extra choice.
            foreach(var later in state.Choices.Keys.Where(key=>key.Effect>binding.Effect||key.Effect==binding.Effect&&key.Execution>binding.Execution).ToArray())state.Choices.Remove(later);
            if(state.Choices.Count>=64&&!state.Choices.ContainsKey(key)){state.CountingStopped=true;return;}
            var rules=selected.Select(id=>Pack(request.DecisionOptions[id],1024)).ToArray();
            state.Choices[key]=new(binding.Effect,binding.Execution,rules);
            // Keep rule fingerprints for all 64 observations, but verbose bodies for only the last eight choices.
            foreach(var old in state.Choices.OrderByDescending(pair=>pair.Key).Skip(8).ToArray())
                state.Choices[old.Key]=old.Value with{Rules=old.Value.Rules.Select(rule=>rule with{Body=null,Bytes=0,Omission="rule-retention-budget"}).ToArray()};
        }
    }
    private static Catalog Unknown(string reason,int budget)=>new(false,reason,null,null,null,null,null,null,null,[],null,0,budget,[]);
    internal static Catalog Read(GameState game,PlayerChoiceRequest request,int byteLimit=4096)
    {
        if(byteLimit<0)throw new ArgumentOutOfRangeException(nameof(byteLimit));
        if(!Bindings.TryGetValue(request,out var binding))return Unknown("native-invocation-unavailable",byteLimit);
        var bindingCurrent=ReferenceEquals(binding.Game,game)&&binding.Revision==game.StateRevision
            &&game.GetPlayerState(request.RequestingPlayerId)?.TurnNumber==binding.Turn;
        var nativeAction=bindingCurrent?binding.NativeAction.Fact:null;
        if(!Valid(game,request,binding))return Unknown("native-invocation-unavailable",byteLimit) with{NativeAction=nativeAction,
            NativeActionOmission=bindingCurrent?binding.NativeAction.Omission:"stale-native-request"};
        var parent=Pack(binding.Config);var state=States.GetOrCreateValue(game);Proposed[] previous;bool countingStopped;
        lock(state){
            Prepare(game,request,binding,parent.Fingerprint);
            previous=state.Choices.Values.Where(choice=>choice.Effect<binding.Effect||choice.Effect==binding.Effect&&choice.Execution<binding.Execution)
                .OrderBy(choice=>choice.Effect).ThenBy(choice=>choice.Execution).ToArray();countingStopped=state.CountingStopped;
        }
        var bytes=0;var omitted=new List<string>();JsonElement? parentRule=null;
        if(parent.Body is {} body&&parent.Bytes<=byteLimit){parentRule=body;bytes+=parent.Bytes;}else omitted.Add(parent.Omission??"parent-rule-byte-budget");
        var retained=previous.TakeLast(4).ToArray();var packed=new List<Prior>();
        var currentRules=request.SelectableTargetIds.Select((id,index)=>(Index:index,Hash:request.DecisionOptions.TryGetValue(id,out var config)?Pack(config,0).Fingerprint:null))
            .Where(item=>item.Hash!=null).ToArray();
        foreach(var choice in retained.Reverse()){
            var full=choice.Rules.All(rule=>rule.Body!=null)&&choice.Rules.Sum(rule=>rule.Bytes)<=byteLimit-bytes;
            if(full)bytes+=choice.Rules.Sum(rule=>rule.Bytes);
            packed.Add(new(choice.Effect,choice.Execution,choice.Rules.Select(rule=>rule.Fingerprint).ToArray(),
                full?choice.Rules.Select(rule=>rule.Body!.Value).ToArray():null,full?null:"prior-rule-unavailable-or-byte-budget",
                choice.Rules.Select(rule=>currentRules.Where(current=>rule.Fingerprint!=null&&current.Hash==rule.Fingerprint).Select(current=>current.Index).ToArray()).ToArray()));
        }
        packed.Reverse();
        var phase=binding.Preflight?"preflight":"commit";
        var earlierRepeatsKnown=Enumerable.Range(0,binding.Execution).All(index=>previous.Any(choice=>choice.Effect==binding.Effect&&choice.Execution==index));
        var enabledEffects=binding.Config.Effects.Where(effect=>effect.IsEnabled).ToArray();
        var earlierEffectsKnown=enabledEffects.Take(binding.Effect).Select((effect,index)=>(effect,index)).Where(item=>item.effect.Type==ShadowverseBeyond.Server.Domain.Effects.EffectType.Choose)
            .All(item=>item.effect.RepeatCountValue==null&&item.effect.RepeatCondition==null&&item.effect.RepeatCount is >=0 and <64
                &&Enumerable.Range(0,item.effect.RepeatCount+1).All(index=>previous.Any(choice=>choice.Effect==item.index&&choice.Execution==index)));
        var stamp=new AiChoiceFlowStamp(phase,binding.Effect,binding.Execution,parent.Fingerprint,!countingStopped&&parent.Fingerprint!=null&&earlierRepeatsKnown&&earlierEffectsKnown
            &&previous.All(choice=>choice.Rules.All(rule=>rule.Fingerprint!=null)),
            previous.Length,retained.Length,previous.Length-retained.Length,retained.Select(choice=>new AiPriorChoiceStage(choice.Effect,choice.Execution,
                choice.Rules.Select(rule=>rule.Fingerprint??"unknown").ToArray())).ToArray()){NativeAction=nativeAction};
        var references=new SortedSet<int>();foreach(var rule in packed.SelectMany(choice=>choice.Rules??[]).Concat(parentRule==null?[]:[parentRule.Value]))references.UnionWith(AiDefinitionReferences.Extract(rule).CardIds);
        return new(true,null,phase,binding.Source.CurrentDefinitionId,binding.Source.OwnerPlayerId==request.RequestingPlayerId?"own":"enemy",binding.Effect,binding.Execution,
            parent.Fingerprint,parentRule,packed.ToArray(),stamp,bytes,byteLimit,omitted.ToArray()){ReferencedCardIds=references.Take(32).ToArray(),OmittedReferenceCount=Math.Max(0,references.Count-32),NativeAction=nativeAction,NativeActionOmission=binding.NativeAction.Omission};
    }
    internal static string StageKey(AiChoiceFlowStamp? stamp)
    {
        if(stamp is not {Schema:1,Phase:"preflight" or "commit",EffectIndex:>=0 and <=1024,ExecutionIndex:>=0 and <=1024,
            ParentRulesFingerprint:{Length:64} hash}||!hash.All(Uri.IsHexDigit)||stamp.PriorChoices is not {Length:<=4})return "unknown";
        if(stamp.PriorChoices.Any(choice=>choice==null||choice.RuleFingerprints is not {Length:<=12}
            ||choice.RuleFingerprints.Any(value=>value==null||value!="unknown"&&(value.Length!=64||!value.All(Uri.IsHexDigit)))))return "unknown";
        var prior=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{stamp.PriorChoicesKnown,
            stamp.PriorChoicesObserved,stamp.PriorChoicesOmitted,stamp.PriorChoices,nativeAction=stamp.NativeAction==null?null:new{
                stamp.NativeAction.Kind,stamp.NativeAction.ActorSide,actorId=stamp.NativeAction.Actor?.CardId,stamp.NativeAction.PlayMode,
                stamp.NativeAction.PaidCostQuote,stamp.NativeAction.PaidCostSpecified,stamp.NativeAction.RequestedAbilityDefinitionId}},Json))).ToLowerInvariant();
        return $"{stamp.Phase}:{stamp.EffectIndex}:{stamp.ExecutionIndex}:{hash}:{(stamp.PriorChoicesKnown?"known":"partial")}:{prior}";
    }
}
