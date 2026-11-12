using System.Text.RegularExpressions;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Services;

internal static class AiRuleContext
{
    private static readonly Regex Reference = new(@"『([^』]+)』|「([^」]+)」", RegexOptions.Compiled | RegexOptions.NonBacktracking);
    internal sealed record CardRule(int Id, string Name, int Cost, int? Attack, int? Health,
        string Type, IReadOnlyList<string> Keywords, string Text, string Class, IReadOnlyList<string> Tribe, int? Countdown, bool IsCollectible);
    internal sealed record CardMetadata(int Id, string Class, IReadOnlyList<string> Tribe,
        int? Countdown, bool IsCollectible, IReadOnlyList<string> Keywords);
    internal static CardMetadata[] Metadata(IEnumerable<int> ids, IReadOnlyDictionary<int, CardDefinition> definitions) =>
        ids.Distinct().Order().Where(definitions.ContainsKey).Select(id => definitions[id])
            .Select(card => new CardMetadata(card.Id, card.Class.ToString(), card.Tribe ?? [], card.Countdown,
                card.IsCollectible, card.Keywords ?? [])).ToArray();
    internal sealed record ConfigurationGap(int CardId,string Reason);
    internal sealed record ReferenceCatalog(CardRule[] Cards, string[] UnresolvedNames, string[] OmittedNames,
        int UnresolvedCount, int OmittedCount,int[] UnresolvedIds,int[] OmittedIds,int UnresolvedIdCount,int OmittedIdCount,
        ConfigurationGap[] ConfigurationGaps,int ConfigurationGapCount,int ConfigurationScanCount,int ConfigurationScanLimit);

    internal static CardRule Describe(CardDefinition card) => new(card.Id, card.Name, card.Cost, card.Attack, card.Health,
        card.Type.ToString(), card.Keywords ?? [], card.Description,
        card.Class.ToString(), card.Tribe ?? [], card.Countdown, card.IsCollectible);

    private static IEnumerable<string> Names(IEnumerable<string> texts) => texts
        .SelectMany(text => Reference.Matches(text ?? "").Select(match =>
            match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value))
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

    internal static ReferenceCatalog Collect(IEnumerable<string> descriptions,
        IReadOnlyDictionary<int, CardDefinition> definitions, int limit = 40)=>
        CollectCore(descriptions,definitions,[],null,limit,0);

    internal static ReferenceCatalog CollectConfigured(IEnumerable<string> descriptions,
        IReadOnlyDictionary<int,CardDefinition> definitions,IEnumerable<int> roots,
        Func<int,AiDefinitionReferences.Links> dependencies,int limit=40,int scanLimit=256,IEnumerable<int>? providedIds=null)=>
        CollectCore(descriptions,definitions,roots,dependencies,limit,scanLimit,providedIds);

    private static ReferenceCatalog CollectCore(IEnumerable<string> descriptions,
        IReadOnlyDictionary<int,CardDefinition> definitions,IEnumerable<int> roots,
        Func<int,AiDefinitionReferences.Links>? dependencies,int limit,int scanLimit,IEnumerable<int>? providedIds=null)
    {
        if(limit<0||limit>128)throw new ArgumentOutOfRangeException(nameof(limit));
        if(scanLimit<0||scanLimit>1024)throw new ArgumentOutOfRangeException(nameof(scanLimit));
        var rootIds=roots.Where(id=>id>0).ToHashSet();
        var knownIds=(providedIds??rootIds).ToHashSet();
        var index=definitions.Values.GroupBy(card=>card.Name,StringComparer.Ordinal)
            .ToDictionary(group=>group.Key,group=>group.OrderBy(card=>card.Id).ToArray(),StringComparer.Ordinal);
        var pendingNames=new Queue<string>(Names(descriptions));
        var pendingIds=new Queue<int>(rootIds.Order());
        var visitedNames=new HashSet<string>(StringComparer.Ordinal);var visitedIds=new HashSet<int>();
        var unresolved=new SortedSet<string>(StringComparer.Ordinal);var omitted=new SortedSet<string>(StringComparer.Ordinal);
        var unresolvedIds=new SortedSet<int>();var omittedIds=new SortedSet<int>();
        var gaps=new List<ConfigurationGap>();var scans=0;
        var found=new SortedDictionary<int,CardRule>();
        void Visit(int id) {
            if(!visitedIds.Add(id))return;
            if(!definitions.TryGetValue(id,out var card)){unresolvedIds.Add(id);return;}
            if(!knownIds.Contains(id)) {
                if(found.Count>=limit){omitted.Add(card.Name);omittedIds.Add(id);}
                else found.Add(id,Describe(card));
            }
            foreach(var nested in Names([card.Description]))pendingNames.Enqueue(nested);
            if(dependencies==null)return;
            if(scans>=scanLimit){gaps.Add(new(id,"configuration-scan-budget"));return;}
            scans++;
            var links=dependencies(id);
            foreach(var next in links.CardIds)pendingIds.Enqueue(next);
            if(!links.Available)gaps.Add(new(id,"configuration-unavailable"));
            else if(links.UnresolvedCount>0)gaps.Add(new(id,"unresolved-reference-parameters:"+links.UnresolvedCount));
        }
        while(pendingNames.Count>0||pendingIds.Count>0) {
            if(pendingIds.TryDequeue(out var id)){Visit(id);continue;}
            var name=pendingNames.Dequeue();
            if(!visitedNames.Add(name))continue;
            if(!index.TryGetValue(name,out var cards)){unresolved.Add(name);continue;}
            foreach(var card in cards)Visit(card.Id);
        }
        // Counts disclose overflows; labels and IDs are bounded pointers, not substitute rules.
        return new(found.Values.ToArray(),unresolved.Take(40).Select(name=>name.Length<=200?name:name[..200]).ToArray(),
            omitted.Take(40).ToArray(),unresolved.Count,omitted.Count,unresolvedIds.Take(40).ToArray(),omittedIds.Take(40).ToArray(),
            unresolvedIds.Count,omittedIds.Count,gaps.Take(40).ToArray(),gaps.Count,scans,scanLimit);
    }
}
