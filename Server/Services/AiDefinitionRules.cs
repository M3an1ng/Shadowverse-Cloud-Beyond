using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public sealed record AiDefinitionStamp(string Fingerprint,string EngineFingerprint,int Cards,int Abilities,int Buffs,
    bool Available,string[] Issues,
    string Scope="Loaded public card/ability/buff definitions, not runtime instance effects or an execution simulation.");

// Global pools and dynamic definition lookups can depend on cards outside the starting deck.
// Hash the loaded catalog once per published provider revision; emit only the digest, never raw editor metadata.
internal static class AiDefinitionRules
{
    private sealed class Holder { internal Guid Revision; internal AiDefinitionStamp? Stamp; }
    private static readonly ConditionalWeakTable<IGameDataProvider,Holder> Cache=new();
    private static readonly string Engine=AiEngineRulesIdentity.Current.Fingerprint;
    private static readonly JsonSerializerOptions ConfigJson=CreateConfigJson();
    private static JsonSerializerOptions CreateConfigJson()
    {
        var resolver=new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info=> {
            if(info.Type!=typeof(AbilityConfig))return;
            // Id is ignored by the editor JSON converter, but runtime trigger limits use it.
            var property=info.CreateJsonPropertyInfo(typeof(long),"definitionId");
            property.Get=value=>((AbilityConfig)value).Id;info.Properties.Add(property);
        });
        return new(JsonSerializerDefaults.Web) {TypeInfoResolver=resolver,Converters={new JsonStringEnumConverter()}};
    }

    private static Guid Revision(IGameDataProvider provider)
    {
        try {return provider.DataRevision;}
        catch(Exception error) when(error is NullReferenceException or InvalidCastException) {return Guid.Empty;}
    }
    internal static AiDefinitionStamp Read(IGameDataProvider provider)
    {
        var holder=Cache.GetValue(provider,_=>new());
        lock(holder)
        {
            var revision=Revision(provider);
            if(revision!=Guid.Empty&&revision==holder.Revision&&holder.Stamp!=null)return holder.Stamp;
            for(var attempt=0;attempt<2;attempt++)
            {
                var stamp=ReadCatalog(provider);
                var after=Revision(provider);
                if(revision==after)
                {
                    if(after!=Guid.Empty&&stamp.Available){holder.Revision=after;holder.Stamp=stamp;}
                    return stamp;
                }
                revision=after;
            }
            return new("",Engine,0,0,0,false,["catalog-changed-during-read"]);
        }
    }
    private static AiDefinitionStamp ReadCatalog(IGameDataProvider provider)
    {
        var issues=new SortedSet<string>(StringComparer.Ordinal);
        var cards=new List<object>();var buffs=new List<object>();var abilities=0;
        try
        {
            var definitions=provider.GetAllCardDefinitions();
            if(definitions==null||definitions.Count==0)return new("",Engine,0,0,0,false,["card-catalog-unavailable"]);
            var allConfigs=provider.GetAllAbilityConfigs();
            var configMap=allConfigs??definitions.Keys.ToDictionary(id=>id,id=>provider.GetAbilityConfigs(id));
            foreach(var id in definitions.Keys.Concat(configMap.Keys).Distinct().Order())
            {
                definitions.TryGetValue(id,out var card);
                if(!configMap.TryGetValue(id,out var configs))configs=provider.GetAbilityConfigs(id);
                if(configs==null){issues.Add("ability-config-unavailable:"+id);configs=[];}
                abilities+=configs.Count;
                try {
                    // No art, editor, timestamps, account information or navigation properties.
                    var publicCard=card==null?null:(object)new {
                        rule=AiRuleContext.Describe(card),rarity=card.Rarity.ToString(),card.CardSet,card.CardSetDefinitionId,card.AbilityIds,
                        set=card.CardSetDefinition==null?null:new {card.CardSetDefinition.Id,card.CardSetDefinition.Name,card.CardSetDefinition.Abbreviation,card.CardSetDefinition.Region,card.CardSetDefinition.Sequence,card.CardSetDefinition.Environments}
                    };
                    cards.Add(new {id,hash=Hash(JsonSerializer.SerializeToElement(new {card=publicCard,abilities=configs},ConfigJson))});
                }
                catch(Exception error) when(error is JsonException or NotSupportedException or InvalidOperationException)
                {issues.Add("ability-config-unserializable:"+id);}
            }
            var allBuffs=provider.GetAllBuffDefinitions();
            if(allBuffs!=null)
                foreach(var pair in allBuffs.OrderBy(pair=>pair.Key,StringComparer.Ordinal))
                {
                    var buff=pair.Value;
                    try {
                        var config=string.IsNullOrWhiteSpace(buff.JsonConfig)?JsonSerializer.SerializeToElement<object?>(null)
                            :ParseConfig(buff.JsonConfig);
                        buffs.Add(new {id=pair.Key,hash=Hash(JsonSerializer.SerializeToElement(new {
                            buff.Id,buff.Name,buff.Description,buff.BuffType,config,buff.DslExpression,
                            buff.DefaultDurationType,buff.DefaultDurationParameter,buff.Priority,buff.IsActive
                        },ConfigJson))});
                    } catch(JsonException){issues.Add("buff-config-unserializable:"+pair.Key);}
                }
            else
            {
                // Providers that cannot enumerate buffs may still support fixtures without external templates.
                // Otherwise do not claim a verified catalog or reuse a paid guide under an incomplete identity.
                var raw=JsonSerializer.Serialize(configMap,ConfigJson);
                if(raw.Contains("BuffDefinitionId",StringComparison.OrdinalIgnoreCase))issues.Add("buff-catalog-unavailable");
            }
        }
        catch(Exception error) when(error is IOException or InvalidOperationException or JsonException
            or NotSupportedException or NullReferenceException)
        {issues.Add("catalog-read-failed:"+error.GetType().Name);}
        var fingerprint=Hash(JsonSerializer.SerializeToElement(new {cards,buffs},ConfigJson));
        return new(fingerprint,Engine,cards.Count,abilities,buffs.Count,issues.Count==0,issues.ToArray());
    }
    private static JsonElement ParseConfig(string json)
    {using var document=JsonDocument.Parse(json);return document.RootElement.Clone();}

    private static string Hash(JsonElement json)
    {
        using var bytes=new MemoryStream();using(var writer=new Utf8JsonWriter(bytes))WriteCanonical(writer,json);
        return Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
    }
    private static void WriteCanonical(Utf8JsonWriter writer,JsonElement json)
    {
        if(json.ValueKind==JsonValueKind.Object)
        {
            writer.WriteStartObject();foreach(var property in json.EnumerateObject().OrderBy(property=>property.Name,StringComparer.Ordinal))
            {writer.WritePropertyName(property.Name);WriteCanonical(writer,property.Value);}writer.WriteEndObject();
        }
        else if(json.ValueKind==JsonValueKind.Array)
        {writer.WriteStartArray();foreach(var value in json.EnumerateArray())WriteCanonical(writer,value);writer.WriteEndArray();}
        else json.WriteTo(writer);
    }

    internal static string Compatibility(AiDefinitionStamp? recorded,AiDefinitionStamp current,bool changedDuringObservation=false)
    {
        if(changedDuringObservation)return "mixed-observed-configs";
        if(recorded?.Available!=true||!current.Available)return "unknown";
        if(recorded.Fingerprint!=current.Fingerprint)return "config-changed";
        if(!AiEngineRulesIdentity.Equivalent(recorded.EngineFingerprint,current.EngineFingerprint))return "engine-changed";
        return "same-config-and-engine";
    }
}
