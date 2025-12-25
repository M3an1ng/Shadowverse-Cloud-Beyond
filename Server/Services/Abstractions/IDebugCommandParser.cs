using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    public sealed record DebugCommandParseResult(
        bool Success,
        string? ErrorMessage,
        IReadOnlyList<IGameEvent> GeneratedEvents)
    {
        public static DebugCommandParseResult Ok(IReadOnlyList<IGameEvent> events) => new(true, null, events);
        public static DebugCommandParseResult Fail(string message) => new(false, message, Array.Empty<IGameEvent>());
    }

    public interface IDebugCommandParser
    {
        DebugCommandParseResult Parse(GameState gameState, Guid requestingPlayerId, DebugCommandDto dto);
    }
}

