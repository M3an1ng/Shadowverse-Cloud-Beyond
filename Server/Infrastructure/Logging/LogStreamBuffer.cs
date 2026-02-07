using System.Collections.Concurrent;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Infrastructure.Logging;

public class LogStreamBuffer
{
    private readonly ConcurrentQueue<LogStreamEntryDto> _entries = new();
    private readonly int _maxEntries;

    public LogStreamBuffer(int maxEntries = 2000)
    {
        _maxEntries = Math.Max(100, maxEntries);
    }

    public void Append(LogStreamEntryDto entry)
    {
        _entries.Enqueue(entry);
        TrimToLimit();
    }

    public IReadOnlyList<LogStreamEntryDto> Snapshot(int take)
    {
        var snapshot = _entries.ToArray();
        if (snapshot.Length == 0)
        {
            return Array.Empty<LogStreamEntryDto>();
        }

        if (take <= 0)
        {
            return Array.Empty<LogStreamEntryDto>();
        }

        var count = Math.Min(take, snapshot.Length);
        return snapshot[^count..];
    }

    private void TrimToLimit()
    {
        while (_entries.Count > _maxEntries)
        {
            _entries.TryDequeue(out _);
        }
    }
}
