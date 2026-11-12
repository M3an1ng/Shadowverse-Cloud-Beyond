using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

// Configuration facts are not predictions. DSL is preserved without evaluating or rewriting it.
internal static class AiAbilityFacts
{
    internal sealed record Omission(int CardId,string Reason);
    internal sealed record Catalog(JsonElement[] Cards,Omission[] Omitted,int Requested,int ContentBytes,int ByteLimit,
        string Source="Loaded public definitions; not runtime grants, evaluated DSL or simulated outcomes.");
    private sealed record CardFact(JsonElement Json,int Bytes);
    private sealed class Holder {internal Guid Revision;internal readonly Dictionary<int,CardFact> Cards=new();}
    private static readonly ConditionalWeakTable<IGameDataProvider,Holder> Cache=new();
    private static readonly JsonSerializerOptions Json=CreateJson();
    private static JsonSerializerOptions CreateJson() {
        var resolver=new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info=>{
            if(info.Type!=typeof(AbilityConfig))return;
            var property=info.CreateJsonPropertyInfo(typeof(long),"definitionId");
            property.Get=value=>((AbilityConfig)value).Id;info.Properties.Add(property);
        });
        return new(JsonSerializerDefaults.Web){Encoder=JavaScriptEncoder.Create(UnicodeRanges.All),
            TypeInfoResolver=resolver,Converters={new JsonStringEnumConverter()}};
    }
    private static Guid Revision(IGameDataProvider provider) {
        try{return provider.DataRevision;}catch(Exception error) when(error is NullReferenceException or InvalidCastException){return Guid.Empty;}
    }
    internal static Catalog Read(IGameDataProvider provider,IEnumerable<int> cardIds,int byteLimit=49152)
    {
        if(byteLimit<0)throw new ArgumentOutOfRangeException(nameof(byteLimit));
        // Caller supplies priority: deck first, then referenced definitions; preserve it without duplicates.
        var ids=cardIds.Where(id=>id>0).Distinct().ToArray();
        var facts=new List<JsonElement>();var omitted=new List<Omission>();var bytes=0;
        var holder=Cache.GetValue(provider,_=>new());
        lock(holder) {
            var revision=Revision(provider);
            if(revision==Guid.Empty||holder.Revision!=revision){holder.Cards.Clear();holder.Revision=revision;}
            foreach(var id in ids) {
                try {
                    if(!holder.Cards.TryGetValue(id,out var card)) {
                        if(provider.GetCardDefinition(id)==null){omitted.Add(new(id,"definition-unavailable"));continue;}
                        var abilities=provider.GetAbilityConfigs(id);
                        if(abilities==null){omitted.Add(new(id,"abilities-unavailable"));continue;}
                        var packed=abilities.Select(ability=>Ability(ability,new(ReferenceEqualityComparer.Instance))).ToArray();
                        var config=JsonSerializer.SerializeToElement(packed,Json);
                        var templateIds=new SortedSet<string>(StringComparer.Ordinal);
                        FindTemplateIds(config,templateIds);
                        var templates=provider.GetAllBuffDefinitions();
                        var included=new List<object>();var unavailable=new List<string>();
                        foreach(var templateId in templateIds) {
                            if(templates?.TryGetValue(templateId,out var template)==true) {
                                using var document=JsonDocument.Parse(string.IsNullOrWhiteSpace(template.JsonConfig)?"null":template.JsonConfig);
                                included.Add(new {id=templateId,template.Name,template.Description,template.BuffType,
                                    config=document.RootElement.Clone(),template.DslExpression,template.DefaultDurationType,
                                    template.DefaultDurationParameter,template.Priority,template.IsActive});
                            } else unavailable.Add(templateId);
                        }
                        var body=new Dictionary<string,object?>{["cardId"]=id,["abilities"]=packed};
                        if(included.Count>0)body["buffTemplates"]=included;
                        if(unavailable.Count>0)body["unavailableBuffTemplates"]=unavailable;
                        var element=JsonSerializer.SerializeToElement(body,Json);
                        card=new(element,JsonSerializer.SerializeToUtf8Bytes(element,Json).Length);
                        if(revision!=Guid.Empty)holder.Cards[id]=card;
                    }
                    // Whole cards only. Never clip an expression, a mode, or a nested effect silently.
                    if(card.Bytes>byteLimit-bytes){omitted.Add(new(id,"byte-budget"));continue;}
                    facts.Add(card.Json);bytes+=card.Bytes;
                }
                catch(Exception error) when(error is JsonException or InvalidOperationException or NotSupportedException or NullReferenceException)
                {omitted.Add(new(id,"configuration-unreadable"));}
            }
            if(Revision(provider)!=revision) {
                holder.Cards.Clear();
                return new([],ids.Select(id=>new Omission(id,"catalog-changed-during-read")).ToArray(),ids.Length,0,byteLimit);
            }
        }
        return new(facts.ToArray(),omitted.ToArray(),ids.Length,bytes,byteLimit);
    }
    private static void FindTemplateIds(JsonElement json,SortedSet<string> ids)
    {
        if(json.ValueKind==JsonValueKind.Array){foreach(var item in json.EnumerateArray())FindTemplateIds(item,ids);}
        else if(json.ValueKind==JsonValueKind.Object)foreach(var property in json.EnumerateObject()) {
            if(property.Name.Equals("BuffDefinitionId",StringComparison.OrdinalIgnoreCase)
                &&property.Value.ValueKind==JsonValueKind.String&&property.Value.GetString() is {Length:>0} plainId)ids.Add(plainId);
            if(property.Name.Equals("BuffDefinitionId",StringComparison.OrdinalIgnoreCase)
                &&property.Value.ValueKind==JsonValueKind.Object) {
                foreach(var literal in property.Value.EnumerateObject().Where(item=>item.Name.Equals("value",StringComparison.OrdinalIgnoreCase)))
                    if(literal.Value.ValueKind==JsonValueKind.String&&literal.Value.GetString() is {Length:>0} id)ids.Add(id);
            }
            FindTemplateIds(property.Value,ids);
        }
    }
    internal static JsonElement Configuration(AbilityConfig ability)=>JsonSerializer.SerializeToElement(Ability(ability,new(ReferenceEqualityComparer.Instance)),Json);

    private static Dictionary<string,object?> Ability(AbilityConfig ability,HashSet<AbilityConfig> visiting)
    {
        if(visiting.Count>=48||!visiting.Add(ability))throw new InvalidOperationException("Cyclic/deep ability configuration");
        try {
            var fact=new Dictionary<string,object?> { ["id"]=ability.Id,["type"]=ability.Type.ToString() };
            if(ability.CardId!=0)fact["cardId"]=ability.CardId;
            if(!string.IsNullOrEmpty(ability.Name))fact["name"]=ability.Name;
            if(!string.IsNullOrEmpty(ability.Description))fact["text"]=ability.Description;
            if(!ability.IsEnabled)fact["enabled"]=false;
            if(ability.AbilityKeywords.Count>0)fact["keywords"]=ability.AbilityKeywords;
            if(ability.ActivateCost.HasValue)fact["legacyEnhanceCost"]=ability.ActivateCost;
            if(ability.AlternatePlay!=null)fact["alternatePlay"]=ability.AlternatePlay;
            var trigger=ability.Trigger;
            var when=new Dictionary<string,object?> {
                ["events"]=trigger.EventTypes.Select(value=>value.ToString()).ToArray(),
                ["relation"]=trigger.RelationType.ToString(),["timing"]=trigger.Timing.ToString()
            };
            if(!trigger.IsEnabled)when["enabled"]=false;
            if(trigger.ValidZones.Count>0)when["zones"]=trigger.ValidZones.Select(value=>value.ToString()).ToArray();
            if(trigger.RevalidateZoneBeforeExecution)when["recheckZone"]=true;
            if(trigger.CountLimit!=null)when["limit"]=trigger.CountLimit;
            if(trigger.LimitSameNameOncePerTransaction)when["oncePerTransaction"]=true;
            fact["trigger"]=when;
            if(ability.PreConditions.Count>0)fact["preconditions"]=ability.PreConditions.Select(Condition).ToArray();
            if(ability.Conditions.Count>0)fact["conditions"]=ability.Conditions.Select(Condition).ToArray();
            if(ability.Targets.Count>0)fact["targets"]=ability.Targets.OrderBy(pair=>pair.Key,StringComparer.Ordinal).ToDictionary(pair=>pair.Key,pair=>{
                var target=pair.Value;
                var result=new Dictionary<string,object?>{["query"]=target.Query,["handling"]=target.GetEffectiveTargetHandlingStrategy().ToString()};
                if(!target.IsEnabled)result["enabled"]=false;
                if(target.Order.HasValue)result["order"]=target.Order;
                if(target.PlayerChoiceOptions!=null)result["choice"]=target.PlayerChoiceOptions;
                return result;
            });
            if(ability.Properties.Count>0)fact["properties"]=Values(ability.Properties);
            if(ability.Effects.Count>0)fact["effects"]=ability.Effects.Select(effect=>Effect(effect,visiting)).ToArray();
            // Auras and alternate forms retain their native schemas, including defaults and legacy fields.
            if(ability.Auras.Count>0)fact["auras"]=ability.Auras;
            if(ability.CounterRegistrations.Count>0)fact["cardCounters"]=ability.CounterRegistrations;
            if(ability.PlayerCounterRegistrations.Count>0)fact["playerCounters"]=ability.PlayerCounterRegistrations;
            return fact;
        }
        finally{visiting.Remove(ability);}
    }
    private static object Condition(ConditionConfig condition)=>condition.IsEnabled
        ?(object)new {dsl=condition.Expression}:new {dsl=condition.Expression,enabled=false};
    private static Dictionary<string,object?> Values(Dictionary<string,ValueConfig> values)=>values
        .OrderBy(pair=>pair.Key,StringComparer.Ordinal).ToDictionary(pair=>pair.Key,pair=>Value(pair.Value));
    private static object? Value(ValueConfig value)=>value switch {
        StaticValueConfig literal=>new {value=literal.Value},
        FormulaValueConfig formula=>new {dsl=formula.Formula},
        _=>new {native=value}
    };
    private static object Effect(EffectConfig effect,HashSet<AbilityConfig> visiting)
    {
        var result=new Dictionary<string,object?>{["type"]=effect.Type.ToString()};
        if(!effect.IsEnabled)result["enabled"]=false;
        if(effect.ExecutionOrder!=0)result["order"]=effect.ExecutionOrder;
        if(effect.RepeatCount!=0)result["extraRepeats"]=effect.RepeatCount;
        if(effect.RepeatCountValue!=null)result["extraRepeatsValue"]=Value(effect.RepeatCountValue);
        if(effect.RepeatCondition!=null)result["repeatCondition"]=Condition(effect.RepeatCondition);
        if(effect.Parameters.Count>0)result["params"]=Values(effect.Parameters);
        if(effect.TargetSelector.Count>0)result["targets"]=effect.TargetSelector;
        if(effect.OnFailure!=FailureHandling.CompleteWithFailure)result["onFailure"]=effect.OnFailure.ToString();
        if(effect.Outputs?.Count>0)result["outputs"]=effect.Outputs.OrderBy(pair=>pair.Key,StringComparer.Ordinal).ToDictionary(pair=>pair.Key,pair=>pair.Value);
        if(effect.ChainedAbilities.Count>0)result["then"]=effect.ChainedAbilities.Select(ability=>Ability(ability,visiting)).ToArray();
        return result;
    }
}