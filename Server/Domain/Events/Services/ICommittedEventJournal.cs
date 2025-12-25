using ShadowverseBeyond.Server.Dtos.Replay;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Events.Services
{
    public interface ICommittedEventJournal
    {
        Task<CommittedBundleAppendResult> AppendBundleAsync(
            string gameId,
            Guid bundleId,
            IReadOnlyList<CommittedEventWriteDto> events,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<RecordedCommittedEventDto>> ReadRangeAsync(
            string gameId,
            long fromSeqInclusive,
            int limit,
            CancellationToken cancellationToken = default);

        Task<long> GetHeadSeqAsync(
            string gameId,
            CancellationToken cancellationToken = default);

        Task<RecordedCommittedEventDto?> FindByEventIdAsync(
            string gameId,
            Guid eventId,
            CancellationToken cancellationToken = default);
    }
}
