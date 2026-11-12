using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Services;

// Legal option indices bind to the actual, resolved native branches. Presentation text is not an identity.
internal static class AiDecisionFacts
{
    internal sealed record Binding(int Index,int Rule);
    internal sealed record Omission(int Index,string Reason);
    internal sealed record Catalog(bool Available,JsonElement[] Rules,Binding[] Bindings,Omission[] Omitted,
        int ContentBytes,int ByteLimit,int UnresolvedReferenceCount,
        string Source="Native resolved option configurations; DSL is preserved without evaluation.")
    {
        public string? Fingerprint {get;init;}
        [JsonIgnore] internal int[] ReferencedCardIds {get;init;}=[];
    }
    internal static Catalog Read(PlayerChoiceRequest request,int byteLimit)
    {
        if(byteLimit<0)throw new ArgumentOutOfRangeException(nameof(byteLimit));
        var ids=request.SelectableTargetIds;
        if(request.TargetKey!="decision"||ids.Count is <2 or >12||ids.Any(id=>id==Guid.Empty)||ids.Distinct().Count()!=ids.Count)
            return new(false,[],[],[new(-1,"invalid-native-option-list")],0,byteLimit,0);
        var rules=new List<JsonElement>();var bindings=new List<Binding>();var omitted=new List<Omission>();
        var known=new Dictionary<string,int>(StringComparer.Ordinal);var references=new SortedSet<int>();
        var bytes=0;var unresolved=0;
        for(var index=0;index<ids.Count;index++)
        {
            if(!request.DecisionOptions.TryGetValue(ids[index],out var config)||config==null)
            {omitted.Add(new(index,"native-option-binding-missing"));continue;}
            try
            {
                if(!AiRuntimeAbilityFacts.SafeConfiguration(config))
                {omitted.Add(new(index,"opaque-or-private-option-configuration"));continue;}
                var fact=AiAbilityFacts.Configuration(config);
                if(AiRuntimeAbilityFacts.ContainsPrivateData(fact))
                {omitted.Add(new(index,"instance-or-player-data-in-option-configuration"));continue;}
                var packed=JsonSerializer.SerializeToUtf8Bytes(fact);
                var hash=Convert.ToHexString(SHA256.HashData(packed));
                if(!known.TryGetValue(hash,out var position))
                {
                    if(packed.Length>byteLimit-bytes){omitted.Add(new(index,"option-rule-byte-budget"));continue;}
                    position=rules.Count;known.Add(hash,position);rules.Add(fact);bytes+=packed.Length;
                    var links=AiDefinitionReferences.Extract(fact);unresolved+=links.UnresolvedCount;
                    references.UnionWith(links.CardIds);
                }
                bindings.Add(new(index,position));
            }
            catch(Exception error) when(error is JsonException or InvalidOperationException or NotSupportedException or ArgumentException)
            {omitted.Add(new(index,"option-configuration-unreadable"));}
        }
        return new(omitted.Count==0,rules.ToArray(),bindings.ToArray(),omitted.ToArray(),bytes,byteLimit,unresolved)
            {ReferencedCardIds=references.ToArray(),Fingerprint=omitted.Count==0
                ?Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{rules,bindings}))).ToLowerInvariant():null};
    }
}
