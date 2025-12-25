using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Replay;
using System;

namespace ShadowverseBeyond.Server.Domain.Events.Services
{
    public interface IRawEventGameEventRegistry
    {
        bool TryCreateGameEvent(
            RawEventEnvelopeDto input,
            GameState gameState,
            Guid requestingPlayerId,
            out IGameEvent? gameEvent,
            out string? errorMessage);
    }
}

