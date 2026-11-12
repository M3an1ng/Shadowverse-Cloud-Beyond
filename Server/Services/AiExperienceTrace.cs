namespace ShadowverseBeyond.Server.Services;

public sealed record AiTraceSection(long Observed, int Retained, long Omitted, int Limit, string Retention);
public sealed record AiTraceCoverage(int? FromTurn, int? ThroughTurn,
    Dictionary<string, AiTraceSection> Sections, Dictionary<string, long> EventTotals,
    string Scope = "Instrumented own observations since the planner joined; not a full replay. Hand actions cover play/discard/fusion materials. Ability contexts cover matched AI act/fusion invocations.");

// Memory and prompt sizes remain bounded, without silently discarding the late game.
internal sealed class AiExperienceTrace
{
    private readonly Dictionary<string, AiTraceSection> sections = new();
    private readonly Dictionary<string, long> totals = new();
    private int? fromTurn, throughTurn;

    internal AiExperienceTrace()
    {
        foreach (var (name, limit) in new[] { ("plays", 40), ("handActions", 96), ("abilityActions", 40),
            ("choices", 24), ("targetChoices",48), ("comparisons", 60), ("attackContexts", 64), ("leaderHealth", 48), ("completedActions", 32), ("experienceRetrievals", 12), ("localTacticalBypasses", 24), ("tacticalAdvice", 32) })
            sections[name] = new(0, 0, 0, limit, "opening-half-and-most-recent-half");
    }

    internal void MarkTurn(int turn)
    {
        fromTurn ??= turn;
        throughTurn = turn;
    }

    internal void Observe(string name, long amount = 1) => totals[name] = totals.GetValueOrDefault(name) + amount;

    internal void Capture<T>(string name, List<T> items, T item)
    {
        var section = sections[name];
        if (items.Count == section.Limit) items.RemoveAt(section.Limit / 2);
        items.Add(item);
        var count = section.Observed + 1;
        sections[name] = section with { Observed = count, Retained = items.Count, Omitted = count - items.Count };
    }

    internal AiTraceCoverage Snapshot() => new(fromTurn, throughTurn,
        sections.ToDictionary(pair => pair.Key, pair => pair.Value), new(totals));
}
