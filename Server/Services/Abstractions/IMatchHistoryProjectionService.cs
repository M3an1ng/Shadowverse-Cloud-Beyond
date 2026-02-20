using ShadowverseBeyond.Server.Dtos.SignalR.Events;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    public interface IMatchHistoryProjectionService
    {
        MatchHistorySyncEventDto BuildSnapshot(string gameId);
    }
}
