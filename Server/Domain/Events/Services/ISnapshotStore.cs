using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Replay;
using System.Threading;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Events.Services
{
    public interface ISnapshotStore
    {
        Task SaveSnapshotAsync(
            string gameId,
            long atSeq,
            GameStateDto snapshot,
            CancellationToken cancellationToken = default);

        Task<RecordedSnapshotDto?> GetLatestAsync(
            string gameId,
            CancellationToken cancellationToken = default);

        Task<RecordedSnapshotDto?> GetAtOrBeforeAsync(
            string gameId,
            long atSeq,
            CancellationToken cancellationToken = default);

        Task DeleteForGameAsync(
            string gameId,
            CancellationToken cancellationToken = default);
    }
}

