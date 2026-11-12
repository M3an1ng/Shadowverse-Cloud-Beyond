using System.Collections;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

// Snapshot-scoped facts only. Never evaluate conditions, sample targets, or cache mutable runtime abilities.
internal static class AiRuntimeAbilityFacts
{
    internal sealed record Location(string Side,string Zone,int Index,int CardId);
    internal sealed record Omission(Location Instance,string Reason);
    internal sealed record Catalog(JsonElement[] Instances,Omission[] Omitted,int Requested,int ContentBytes,int ByteLimit,
        int[] ReferencedCardIds,int UnresolvedReferences,int OmittedReferences,bool Available=true,string? Reason=null);
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    private static readonly Regex GuidText=new(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",RegexOptions.IgnoreCase);
    private static readonly HashSet<string> PrivateKeys=new(StringComparer.OrdinalIgnoreCase){"instanceId","ownerPlayerId","playerId","username","email","password","passwordHash","apiKey","userId","accountId","authorization","accessToken"};
    internal static Catalog Unavailable(string reason,int limit=8192)=>new([],[],0,0,limit,[],0,0,false,reason);
    internal static Catalog ForGame(GameState game,GameStateDto snapshot,Guid actor,IEnumerable<CardInstanceDto>? choices=null)
    {
        var context=GameServiceProvider.CurrentGameContext;
        return context==null||!ReferenceEquals(context.CurrentGameState,game)?Unavailable("matching-context-unavailable"):Read(context,snapshot,actor,choices);
    }
    internal static bool ContainsPrivateData(JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.String)return GuidText.IsMatch(value.GetString()??"");
        if(value.ValueKind==JsonValueKind.Array)return value.EnumerateArray().Any(ContainsPrivateData);
        if(value.ValueKind==JsonValueKind.Object)return value.EnumerateObject().Any(property=>PrivateKeys.Contains(property.Name)||ContainsPrivateData(property.Value));
        return false;
    }
    // Reject captured entities/lazy objects before their getters or enumerators can inspect hidden state.
    internal static bool SafeConfiguration(AbilityConfig config)
    {
        var visiting=new HashSet<object>(ReferenceEqualityComparer.Instance);var nodes=0;
        bool Safe(object? value,int depth) {
            if(++nodes>4096||depth>48)return false;
            if(value==null)return true;
            if(value is string text)return text.Length<=65536&&!GuidText.IsMatch(text);
            if(value is Guid)return false;
            var type=value.GetType();
            if(type.IsPrimitive||type.IsEnum||value is decimal)return true;
            if(value is JsonElement json)return json.GetRawText().Length<=65536&&!ContainsPrivateData(json);
            if(!visiting.Add(value))return false;
            try {
                if(value is IDictionary dictionary) {
                    foreach(DictionaryEntry entry in dictionary)
                        if(entry.Key is not string key||PrivateKeys.Contains(key)||!Safe(entry.Value,depth+1))return false;
                    return true;
                }
                if(value is IList list) {foreach(var item in list)if(!Safe(item,depth+1))return false;return true;}
                if(value is ShadowverseBeyond.Server.Domain.Abilities.Common.ValueConfig
                    &&value is not ShadowverseBeyond.Server.Domain.Abilities.Common.StaticValueConfig
                    &&value is not ShadowverseBeyond.Server.Domain.Abilities.Common.FormulaValueConfig)return false;
                var ns=type.Namespace??"";
                if(type.Assembly!=typeof(AbilityConfig).Assembly
                    ||!(ns.StartsWith("ShadowverseBeyond.Server.Domain.Abilities",StringComparison.Ordinal)
                        ||ns.StartsWith("ShadowverseBeyond.Server.Domain.Buffs",StringComparison.Ordinal)
                        ||ns.StartsWith("ShadowverseBeyond.Server.Domain.Query",StringComparison.Ordinal))
                    ||!(type.Name.EndsWith("Config",StringComparison.Ordinal)||type.Name.EndsWith("Operation",StringComparison.Ordinal)))return false;
                return type.GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(property=>property.CanRead&&property.GetIndexParameters().Length==0)
                    .All(property=>!PrivateKeys.Contains(property.Name)&&Safe(property.GetValue(value),depth+1));
            } finally {visiting.Remove(value);}
        }
        return Safe(config,0);
    }
    internal static Catalog Read(GameContext context,GameStateDto snapshot,Guid actor,IEnumerable<CardInstanceDto>? choices=null,int byteLimit=8192)
    {
        if(byteLimit<0)throw new ArgumentOutOfRangeException(nameof(byteLimit));
        if(context.AbilityManager==null||context.BuffManager==null)return Unavailable("runtime-managers-unavailable",byteLimit);
        var revision=context.CurrentGameState.StateRevision;
        var own=snapshot.Players.FirstOrDefault(player=>player.PlayerId==actor);
        var enemy=snapshot.Players.FirstOrDefault(player=>player.PlayerId!=actor);
        if(own==null||enemy==null||context.CurrentGameState.GameId!=snapshot.GameId)return Unavailable("snapshot-mismatch",byteLimit);
        var visible=new List<(CardInstanceDto View,Location At)>();
        void Add(IEnumerable<CardInstanceDto> cards,string side,string zone) {
            var index=0;foreach(var view in cards)visible.Add((view,new(side,zone,index++,view.CardId)));
        }
        Add(own.Hand,"own","hand");Add(own.Field,"own","field");Add(enemy.Field,"enemy","field");
        Add(own.Emblem,"own","emblem");Add(enemy.Emblem,"enemy","emblem");
        if(own.LeaderCard!=null)Add([own.LeaderCard],"own","leader");
        if(enemy.LeaderCard!=null)Add([enemy.LeaderCard],"enemy","leader");
        if(choices!=null)Add(choices,"choice","choice");
        var locations=visible.DistinctBy(item=>item.View.InstanceId).ToDictionary(item=>item.View.InstanceId,item=>item.At);
        var items=new List<JsonElement>();var omissions=new List<Omission>();var bytes=0;
        var references=new SortedSet<int>();var unresolved=0;
        var provider=GameDataProvider.Instance;
        foreach(var (view,at) in visible.DistinctBy(item=>item.View.InstanceId)) {
            try {
                var card=context.CurrentGameState.FindCardInstance(view.InstanceId);
                if(card==null||card.CurrentDefinitionId!=view.CardId||card.OwnerPlayerId!=view.OwnerPlayerId
                    ||(at.Zone!="choice"&&(card.CurrentZone!=view.Zone
                        ||(at.Zone=="hand"&&card.CurrentZone!=CardZone.Hand)
                        ||(at.Zone is "field" or "emblem"&&card.CurrentZone!=(at.Zone=="field"?CardZone.Field:CardZone.Emblem))))) {omissions.Add(new(at,"instance-unavailable-or-changed"));continue;}
                var definitions=provider.GetAbilityConfigs(view.CardId);
                if(definitions==null){omissions.Add(new(at,"base-configurations-unavailable"));continue;}
                var registered=context.AbilityManager.GetAbilitiesForCard(card.InstanceId);
                if(registered.Count>64){omissions.Add(new(at,"runtime-ability-count-limit"));continue;}
                var effective=context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId,context).Select(ability=>ability.RuntimeId).ToHashSet();
                var matched=new HashSet<AbilityConfig>(ReferenceEqualityComparer.Instance);
                var changes=new List<object>();
                var buffs=context.BuffManager.GetBuffsForCard(card.InstanceId);
                var unsupported=registered.Count(ability=>ability is not Ability);
                for(var index=0;index<registered.Count;index++) {
                    if(registered[index] is not Ability ability)continue;
                    var basis=ability.SourceBuffId==null?definitions.FirstOrDefault(config=>!matched.Contains(config)
                        &&(ReferenceEquals(config,ability.Config)||(config.Id>0&&config.Id==ability.Config.Id))):null;
                    if(basis!=null)matched.Add(basis);
                    var safe=SafeConfiguration(ability.Config);
                    var same=basis!=null&&(ReferenceEquals(basis,ability.Config)
                        ||(safe&&SafeConfiguration(basis)&&AiAbilityFacts.Configuration(basis).GetRawText()==AiAbilityFacts.Configuration(ability.Config).GetRawText()));
                    var kind=basis==null?"added":same?"base":"modified";
                    var suppressed=!effective.Contains(ability.RuntimeId);
                    var disabled=!ability.Config.IsEnabled||!ability.TriggerConfig.IsEnabled||!ability.Trigger.IsEnabled;
                    var used=ability.Trigger.TotalTriggerCount>0||ability.TotalTriggerCount>0;
                    if(kind=="base"&&!suppressed&&!disabled&&!card.IsSilenced&&!used)continue;
                    JsonElement? configFacts=null;string? configUnavailable=null;
                    if(kind!="base") {
                        try {
                            if(!safe)configUnavailable="opaque-or-private-runtime-configuration";
                            else {
                                var projected=AiAbilityFacts.Configuration(ability.Config);
                                if(ContainsPrivateData(projected))configUnavailable="instance-or-player-data-in-configuration";
                                else configFacts=projected;
                            }
                        } catch(Exception error) when(error is JsonException or InvalidOperationException or NotSupportedException) {
                            configUnavailable="runtime-configuration-unreadable";
                        }
                    }
                    var originId=ability.SourceBuffId.HasValue?buffs.FirstOrDefault(buff=>buff.IsActive&&buff.Id==ability.SourceBuffId.Value)?.SourceCardInstanceId:null;
                    var origin=originId.HasValue&&locations.TryGetValue(originId.Value,out var location)?location:null;
                    changes.Add(new {ordinal=index,definitionId=ability.DefinitionId,kind,suppressed,disabled,
                        zoneActive=ability.TriggerConfig.IsValidZone(card.CurrentZone),source=origin,
                        triggerCounts=new {turn=ability.Trigger.CurrentTurnTriggerCount,game=ability.Trigger.TotalTriggerCount,
                            abilityGame=ability.TotalTriggerCount,triggeredInCurrentEvent=ability.Trigger.TriggeredThisEvent},
                        config=configFacts,configUnavailable});
                }
                var missing=definitions.Where(config=>config.IsEnabled&&!matched.Contains(config)).Select(config=>new {definitionId=config.Id,reason="not-registered-for-current-instance"}).ToArray();
                if(changes.Count==0&&missing.Length==0&&!card.IsSilenced&&unsupported==0)continue;
                var ownerSide=card.OwnerPlayerId==own.PlayerId?"own":card.OwnerPlayerId==enemy.PlayerId?"enemy":"unknown";
                var oppositeSide=ownerSide=="own"?"enemy":ownerSide=="enemy"?"own":"unknown";
                var element=JsonSerializer.SerializeToElement(new {instance=at,perspective=new{dslOwnerSide=ownerSide,dslOpponentSide=oppositeSide,cardText="owner-relative"},
                    silenced=card.IsSilenced,changes,missingBase=missing,unsupportedRuntimeAbilities=unsupported},Json);
                var size=JsonSerializer.SerializeToUtf8Bytes(element,Json).Length;
                if(size>byteLimit-bytes){omissions.Add(new(at,"byte-budget"));continue;}
                items.Add(element);bytes+=size;
                var links=AiDefinitionReferences.Extract(element);references.UnionWith(links.CardIds);unresolved+=links.UnresolvedCount;
            } catch(Exception error) when(error is InvalidOperationException or JsonException or NotSupportedException or NullReferenceException) {
                omissions.Add(new(at,"runtime-instance-unreadable"));
            }
        }
        if(context.CurrentGameState.StateRevision!=revision)return Unavailable("game-changed-during-runtime-read",byteLimit);
        return new(items.ToArray(),omissions.ToArray(),locations.Count,bytes,byteLimit,references.Take(40).ToArray(),unresolved,Math.Max(0,references.Count-40));
    }
}
