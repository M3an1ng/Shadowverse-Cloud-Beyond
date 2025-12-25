using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Domain.Events.Services
{
    public interface IGameEventRawEventMapper
    {
        string MapToOutputRawEventJson(GameState gameState, IGameEvent gameEvent);
    }
}
