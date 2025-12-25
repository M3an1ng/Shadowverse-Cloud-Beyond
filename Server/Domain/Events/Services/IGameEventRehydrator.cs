using ShadowverseBeyond.Server.Dtos.Replay;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Events.Services
{
    public interface IGameEventRehydrator
    {
        Task<Domain.Events.IGameEvent?> RehydrateAsync(
            string gameId,
            RecordedCommittedEventDto recorded,
            CancellationToken cancellationToken = default);
    }
}

