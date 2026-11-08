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
    internal sealed record ReferenceCatalog(CardRule[] Cards, string[] UnresolvedNames, string[] OmittedNames,
        int UnresolvedCount, int OmittedCount);

    internal static CardRule Describe(CardDefinition card) => new(card.Id, card.Name, card.Cost, card.Attack, card.Health,
        card.Type.ToString(), card.Keywords ?? [], card.Description,
        card.Class.ToString(), card.Tribe ?? [], card.Countdown, card.IsCollectible);

    private static IEnumerable<string> Names(IEnumerable<string> texts) => texts
        .SelectMany(text => Reference.Matches(text ?? "").Select(match =>
            match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value))
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

    internal static ReferenceCatalog Collect(IEnumerable<string> descriptions,
        IReadOnlyDictionary<int, CardDefinition> definitions, int limit = 40)
    {
        if (limit < 0 || limit > 128) throw new ArgumentOutOfRangeException(nameof(limit));
        var index = definitions.Values.GroupBy(card => card.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(card => card.Id).ToArray(), StringComparer.Ordinal);
        var pending = new Queue<string>(Names(descriptions));
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);
        var omitted = new SortedSet<string>(StringComparer.Ordinal);
        var found = new SortedDictionary<int, CardRule>();
        while (pending.TryDequeue(out var name))
        {
            if (!visited.Add(name)) continue;
            if (!index.TryGetValue(name, out var cards)) { unresolved.Add(name); continue; }
            foreach (var card in cards)
            {
                if (found.ContainsKey(card.Id)) continue;
                if (found.Count >= limit) omitted.Add(name);
                else found.Add(card.Id, Describe(card));
                foreach (var nested in Names([card.Description])) pending.Enqueue(nested);
            }
        }
        // Labels identify missing context; they are not substitute card rules. Counts disclose overflow.
        return new(found.Values.ToArray(), unresolved.Take(40).Select(name => name.Length <= 200 ? name : name[..200]).ToArray(),
            omitted.Take(40).ToArray(), unresolved.Count, omitted.Count);
    }
}
