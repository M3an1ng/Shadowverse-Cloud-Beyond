using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Hubs;

internal sealed class PlayerConnectionTracker
{
    private readonly object _sync = new();
    private readonly Dictionary<string, HashSet<string>> _connectionsByPlayer = new(StringComparer.Ordinal);

    public void Register(Guid gameId, Guid playerId, string connectionId)
    {
        if (gameId == Guid.Empty || playerId == Guid.Empty || string.IsNullOrWhiteSpace(connectionId))
        {
            return;
        }

        var key = BuildKey(gameId, playerId);
        lock (_sync)
        {
            if (!_connectionsByPlayer.TryGetValue(key, out var connections))
            {
                connections = new HashSet<string>(StringComparer.Ordinal);
                _connectionsByPlayer[key] = connections;
            }

            connections.Add(connectionId);
        }
    }

    public bool Unregister(Guid gameId, Guid playerId, string connectionId)
    {
        if (gameId == Guid.Empty || playerId == Guid.Empty || string.IsNullOrWhiteSpace(connectionId))
        {
            return false;
        }

        var key = BuildKey(gameId, playerId);
        lock (_sync)
        {
            if (!_connectionsByPlayer.TryGetValue(key, out var connections))
            {
                return false;
            }

            connections.Remove(connectionId);
            if (connections.Count > 0)
            {
                return true;
            }

            _connectionsByPlayer.Remove(key);
            return false;
        }
    }

    public bool IsConnected(Guid gameId, Guid playerId)
    {
        if (gameId == Guid.Empty || playerId == Guid.Empty)
        {
            return false;
        }

        var key = BuildKey(gameId, playerId);
        lock (_sync)
        {
            return _connectionsByPlayer.TryGetValue(key, out var connections) && connections.Count > 0;
        }
    }

    private static string BuildKey(Guid gameId, Guid playerId)
    {
        return $"{gameId:D}:{playerId:D}";
    }
}
