using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    public interface ITribeRegistry
    {
        string Normalize(string? raw);

        IReadOnlyList<string> NormalizeMany(IEnumerable<string>? rawValues);

        IReadOnlyCollection<string> GetKnownNormalTribes();

        bool TryGetDefinition(string? raw, out TribeDefinition definition);

        Task<IReadOnlyList<string>> NormalizeAndRegisterAsync(IEnumerable<string>? rawValues, CancellationToken cancellationToken = default);

        Task RefreshAsync(CancellationToken cancellationToken = default);
    }
}
